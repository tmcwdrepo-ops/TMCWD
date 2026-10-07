using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class PenaltyService : IPenaltyService
    {
        private readonly UserDbContext _context;

        public PenaltyService(UserDbContext context)
        {
            _context = context;
        }

        public async Task<Penalty> Get(int id)
        {
            var penalty = await _context.Penalties.Where(x => x.Id == id).FirstOrDefaultAsync();
            return penalty;
        }

        public async Task<List<Penalty>> GetAll()
        {
            var penalties = await _context.Penalties.ToListAsync();
            return penalties;
        }

        public async Task<List<Penalty>> GetByReference(string referenceId)
        {
            var penalties = await _context.Penalties.Where(x => x.BillingReferenceId == referenceId).ToListAsync();
            return penalties;
        }

        public async Task<Penalty> SaveUpdate(int userId, Penalty penalty)
        {
            if (penalty.Id != 0)
                throw new InvalidOperationException("Posted penalties cannot be rewritten. Use the authorized waiver workflow.");
            await PostCharges(userId, new() { BillingReferenceIds = new() { penalty.BillingReferenceId } });
            return (await GetByReference(penalty.BillingReferenceId)).Single();
        }

        public async Task<int> WaiveUnpaid(int userId, List<string> billingReferences)
        {
            await RequireRole(userId, UserRole.CustomerServiceOic, UserRole.SuperAdmin);
            return await WaiveUnpaidInternal(userId, billingReferences);
        }

        private async Task<int> WaiveUnpaidInternal(int userId, List<string> billingReferences)
        {
            // A single conditional UPDATE is atomic and cannot turn a paid
            // penalty into a waiver between a read and a later save.
            var now = DateTime.Now;
            return await _context.Penalties
                .Where(p => billingReferences.Contains(p.BillingReferenceId)
                         && (p.PaymentStatus == (int)PaymentStatus.Unpaid
                          || p.PaymentStatus == (int)PaymentStatus.Overdue))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.PaymentStatus, (int)PaymentStatus.Waived)
                    .SetProperty(p => p.UpdatedBy, (long)userId)
                    .SetProperty(p => p.DateUpdated, now));
        }

        private async Task RequireRole(int userId, params UserRole[] allowedRoles)
        {
            var role = await _context.Users.Where(x => x.Id == userId && x.IsActive == true)
                .Select(x => x.Role).SingleOrDefaultAsync();
            if (!role.HasValue || !allowedRoles.Select(x => (int)x).Contains(role.Value))
                throw new UnauthorizedAccessException("Your account is not authorized for this action.");
        }

        public async Task<int> SubmitWaiverRequests(int userId, TMCWD.Model.Billing.SubmitPenaltyWaiverRequest request)
        {
            await RequireRole(userId, UserRole.CustomerRepresentative, UserRole.SuperAdmin);
            if (request?.BillingReferenceIds == null || request.BillingReferenceIds.Count == 0
                || request.BillingReferenceIds.Count > 1000 || request.BillingReferenceIds.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException("Select valid billing references.");

            var reason = request.Reason?.Trim() ?? string.Empty;
            if (reason.Length < 3 || reason.Length > 500)
                throw new InvalidOperationException("Enter a waiver reason between 3 and 500 characters.");

            var references = request.BillingReferenceIds.Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            await using var transaction = await _context.Database.BeginTransactionAsync();
            foreach (var reference in references)
            {
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT Id FROM penalties WHERE BillingReferenceId = {reference} FOR UPDATE");
                var eligible = await _context.Penalties.AnyAsync(p => p.BillingReferenceId == reference
                    && (p.PaymentStatus == (int)PaymentStatus.Unpaid || p.PaymentStatus == (int)PaymentStatus.Overdue));
                var pending = await _context.PenaltyWaiverRequests.AnyAsync(x => x.BillingReferenceId == reference
                    && x.Status == (int)PenaltyWaiverRequestStatus.Pending);
                if (!eligible || pending)
                    throw new InvalidOperationException("A selected penalty is no longer eligible or already has a pending request. Reload the list.");
            }

            var now = DateTime.Now;
            _context.PenaltyWaiverRequests.AddRange(references.Select(reference => new PenaltyWaiverRequest
            {
                BillingReferenceId = reference,
                Reason = reason,
                Status = (int)PenaltyWaiverRequestStatus.Pending,
                RequestedBy = userId,
                DateRequested = now
            }));
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return references.Count;
        }

        public async Task<List<TMCWD.Model.Billing.PenaltyWaiverRequestRecord>> GetPendingWaiverRequests(int userId)
        {
            await RequireRole(userId, UserRole.CustomerServiceOic, UserRole.SuperAdmin);
            var requests = await _context.PenaltyWaiverRequests.AsNoTracking()
                .Where(x => x.Status == (int)PenaltyWaiverRequestStatus.Pending)
                .OrderBy(x => x.DateRequested).ToListAsync();
            var references = requests.Select(x => x.BillingReferenceId).Distinct().ToList();
            var amounts = await _context.Penalties.AsNoTracking()
                .Where(x => references.Contains(x.BillingReferenceId)
                    && (x.PaymentStatus == (int)PaymentStatus.Unpaid || x.PaymentStatus == (int)PaymentStatus.Overdue))
                .GroupBy(x => x.BillingReferenceId)
                .Select(x => new { Reference = x.Key, Amount = x.Sum(p => p.Amount) })
                .ToDictionaryAsync(x => x.Reference, x => x.Amount);
            var accountNumbers = await (from billing in _context.Billings.AsNoTracking()
                                        join account in _context.Accounts.AsNoTracking() on billing.AccountId equals account.Id
                                        where references.Contains(billing.BillingReferenceId)
                                        select new { billing.BillingReferenceId, account.AccountNumber })
                .ToDictionaryAsync(x => x.BillingReferenceId, x => x.AccountNumber);
            var requesterIds = requests.Select(x => x.RequestedBy).Distinct().ToList();
            var names = await _context.Users.AsNoTracking().Where(x => requesterIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name ?? x.Email ?? "Unknown");

            return requests.Select(x => new TMCWD.Model.Billing.PenaltyWaiverRequestRecord
            {
                Id = x.Id,
                BillingReferenceId = x.BillingReferenceId,
                AccountNumber = accountNumbers.GetValueOrDefault(x.BillingReferenceId, string.Empty),
                PenaltyAmount = amounts.GetValueOrDefault(x.BillingReferenceId),
                Reason = x.Reason,
                Status = x.Status,
                RequestedBy = x.RequestedBy,
                RequestedByName = names.GetValueOrDefault(x.RequestedBy, "Unknown"),
                DateRequested = x.DateRequested
            }).ToList();
        }

        public async Task<int> ReviewWaiverRequest(int userId, TMCWD.Model.Billing.ReviewPenaltyWaiverRequest request)
        {
            await RequireRole(userId, UserRole.CustomerServiceOic, UserRole.SuperAdmin);
            if (request == null || request.RequestId <= 0 || (request.Remarks?.Length ?? 0) > 500)
                throw new InvalidOperationException("Invalid waiver review.");

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var waiver = (await _context.PenaltyWaiverRequests
                .FromSqlInterpolated($"SELECT * FROM penalty_waiver_requests WHERE Id = {request.RequestId} FOR UPDATE")
                .ToListAsync()).SingleOrDefault()
                ?? throw new InvalidOperationException("The waiver request no longer exists.");
            if (waiver.Status != (int)PenaltyWaiverRequestStatus.Pending)
                throw new InvalidOperationException("This waiver request has already been reviewed.");

            var affected = 0;
            if (request.Approve)
            {
                affected = await WaiveUnpaidInternal(userId, new List<string> { waiver.BillingReferenceId });
                if (affected == 0)
                    throw new InvalidOperationException("The penalty is no longer unpaid and cannot be waived.");
            }

            waiver.Status = request.Approve
                ? (int)PenaltyWaiverRequestStatus.Approved
                : (int)PenaltyWaiverRequestStatus.Rejected;
            waiver.ReviewedBy = userId;
            waiver.DateReviewed = DateTime.Now;
            waiver.ReviewRemarks = request.Remarks?.Trim();
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return affected;
        }

        private IQueryable<TMCWD.Model.Billing.PenaltyChargeEntry> ChargeEntries(DateTime? billingPeriod, DateTime today, DateTime? dueDateCutoff = null)
        {
            // Use the provided due-date cutoff (≤) or fall back to strictly before today (<)
            var cutoff = dueDateCutoff.HasValue && dueDateCutoff.Value.Date < today
                ? dueDateCutoff.Value.Date : today.AddDays(-1);

            return from bill in _context.Billings
                   join reading in _context.Readings on bill.ReadingId equals reading.Id
                   join sheet in _context.ReadingSheets on reading.ReadingSheetId equals sheet.Id
                   join account in _context.Accounts on bill.AccountId equals account.Id
                   join customer in _context.Customers on account.CustomerId equals customer.Id into customers
                   from customer in customers.DefaultIfEmpty()
                   where sheet.DueDate.HasValue && sheet.DueDate.Value.Date <= cutoff
                       && (bill.PaymentStatus == (int)PaymentStatus.Unpaid || bill.PaymentStatus == (int)PaymentStatus.Overdue)
                       && bill.RemainingAmount > 0 && bill.TotalBillAmount > 0
                       && (!billingPeriod.HasValue || (sheet.BillingDate ?? bill.BillingPeriod).Date == billingPeriod.Value.Date)
                       && !_context.Penalties.Any(p => p.BillingReferenceId == bill.BillingReferenceId)
                   select new TMCWD.Model.Billing.PenaltyChargeEntry
                   {
                       BillingReferenceId = bill.BillingReferenceId,
                       AccountNo = account.AccountNumber,
                       Name = customer == null ? account.AccountNumber : customer.Firstname + " " + customer.Lastname,
                       BillingDate = sheet.BillingDate ?? bill.BillingPeriod,
                       DueDate = sheet.DueDate.Value,
                       Usage = reading.CurrentReading - reading.PreviousReading,
                       InvoiceSnapshotJson = bill.InvoiceSnapshotJson
                   };
        }

        public async Task<List<TMCWD.Model.Billing.PenaltyChargeEntry>> GetChargeEntries(DateTime? billingPeriod, DateTime? dueDate = null)
        {
            var today = TMCWD.Model.Billing.LatePaymentPenalty.Today;
            var entries = await ChargeEntries(billingPeriod, today, dueDate)
                .OrderBy(x => x.DueDate).ThenBy(x => x.BillingReferenceId).ToListAsync();
            foreach (var entry in entries)
                SetWaterPenalty(entry);
            return entries;
        }

        private static void SetWaterPenalty(TMCWD.Model.Billing.PenaltyChargeEntry entry)
        {
            var invoice = entry.InvoiceSnapshotJson == null ? null
                : System.Text.Json.JsonSerializer.Deserialize<TMCWD.Model.Billing.OfficialInvoiceSnapshot>(entry.InvoiceSnapshotJson);
            if (invoice == null)
                throw new InvalidOperationException("A selected bill has no authoritative water-charge snapshot. Its penalty cannot be calculated safely.");
            entry.BillAmount = invoice.BillAmount;
            entry.Penalty = TMCWD.Model.Billing.LatePaymentPenalty.Calculate(entry.BillAmount);
        }

        public Task<List<TMCWD.Model.Billing.ActivePenaltyEntry>> GetActiveEntries(DateTime dueDate)
        {
            var selectedDate = dueDate.Date;
            return (from bill in _context.Billings
                    join reading in _context.Readings on bill.ReadingId equals reading.Id
                    join sheet in _context.ReadingSheets on reading.ReadingSheetId equals sheet.Id
                    join account in _context.Accounts on bill.AccountId equals account.Id
                    join penalty in _context.Penalties on bill.BillingReferenceId equals penalty.BillingReferenceId
                    where sheet.DueDate.HasValue && sheet.DueDate.Value.Date == selectedDate
                        && (penalty.PaymentStatus == (int)PaymentStatus.Unpaid
                         || penalty.PaymentStatus == (int)PaymentStatus.Overdue)
                    group penalty by new
                    {
                        bill.BillingReferenceId,
                        account.AccountNumber,
                        DueDate = sheet.DueDate.Value,
                        Usage = reading.CurrentReading - reading.PreviousReading,
                        bill.TotalBillAmount
                    }
                    into penalties
                    orderby penalties.Key.AccountNumber, penalties.Key.BillingReferenceId
                    select new TMCWD.Model.Billing.ActivePenaltyEntry
                    {
                        BillingReferenceId = penalties.Key.BillingReferenceId,
                        AccountNumber = penalties.Key.AccountNumber,
                        DueDate = penalties.Key.DueDate,
                        Usage = penalties.Key.Usage,
                        BillAmount = penalties.Key.TotalBillAmount,
                        Discount = 0,
                        Penalty = penalties.Sum(p => p.Amount)
                    }).ToListAsync();
        }

        public async Task<TMCWD.Model.Billing.PostPenaltyChargesResult> PostCharges(
            int userId, TMCWD.Model.Billing.PostPenaltyChargesRequest request)
        {
            if (userId <= 0 || request?.BillingReferenceIds == null || request.BillingReferenceIds.Count == 0
                || request.BillingReferenceIds.Count > 1000 || request.BillingReferenceIds.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException("Select valid bills to charge.");

            var references = request.BillingReferenceIds.Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.Ordinal).ToList();
            var today = TMCWD.Model.Billing.LatePaymentPenalty.Today;
            await using var transaction = await _context.Database.BeginTransactionAsync();
            // The same bill locks are used by collection payments. Recheck all
            // eligibility after locking to prevent duplicate or stale postings.
            foreach (var reference in references)
            {
                var bills = await _context.Billings
                    .FromSqlInterpolated($"SELECT * FROM `Billings` WHERE BillingReferenceId = {reference} FOR UPDATE")
                    .ToListAsync();
                if (bills.Count != 1)
                    throw new InvalidOperationException("A selected bill no longer exists. Reload the list.");
            }

            var entries = await ChargeEntries(request.BillingPeriod, today, request.DueDate)
                .Where(x => references.Contains(x.BillingReferenceId)).ToListAsync();
            if (entries.Count != references.Count)
                throw new InvalidOperationException("A selected bill was paid, already charged, or is not overdue. Reload the list; no penalties were posted.");

            decimal total = 0;
            foreach (var entry in entries)
            {
                SetWaterPenalty(entry);
                var amount = entry.Penalty;
                if (amount <= 0)
                    throw new InvalidOperationException("A selected bill has no chargeable amount.");
                _context.Penalties.Add(new Penalty
                {
                    BillingReferenceId = entry.BillingReferenceId,
                    Amount = amount,
                    PaymentStatus = (int)PaymentStatus.Unpaid,
                    CreatedBy = userId, UpdatedBy = userId,
                    DateCreated = DateTime.UtcNow.AddHours(8), DateUpdated = DateTime.UtcNow.AddHours(8)
                });
                total += amount;
            }
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return new TMCWD.Model.Billing.PostPenaltyChargesResult
            {
                PostedCount = entries.Count, TotalPenalty = total, PostingDate = today
            };
        }

    }
}
