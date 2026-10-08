using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class OtherChargeService : IOtherChargeService
    {
        private readonly UserDbContext _context;

        public OtherChargeService(UserDbContext context)
        {
            _context = context;
        }

        public async Task<OtherCharge> Get(int id)
        {
            var otherCharge = await _context.OtherCharges.Where(x => x.Id == id).FirstOrDefaultAsync();
            return otherCharge;
        }

        public async Task<List<OtherCharge>> GetAll()
        {
            var otherCharges = await _context.OtherCharges.ToListAsync();
            return otherCharges;
        }

        public Task<List<OtherCharge>> GetByReference(string referenceId) => GetByReference(referenceId, false);

        public async Task<List<OtherCharge>> GetByReference(string referenceId, bool includeInactive)
        {
            await ValidatePostedPlans(new[] { referenceId });
            var otherCharges = await _context.OtherCharges.AsNoTracking()
                .Where(x => x.BillingReferenceId == referenceId && (includeInactive || x.IsActive))
                .OrderBy(x => x.DateCreated).ThenBy(x => x.Id)
                .ToListAsync();
            foreach (var charge in otherCharges)
                charge.Installments = await (from i in _context.OtherChargeInstallments.AsNoTracking()
                    join p in _context.Payments.AsNoTracking() on i.SettledPaymentId equals (long?)p.Id into receipts
                    from p in receipts.DefaultIfEmpty()
                    where i.OtherChargeId == charge.Id
                    orderby i.InstallmentNumber
                    select new TMCWD.Model.Billing.OtherChargeInstallmentRecord {
                        Id = i.Id, InstallmentNumber = i.InstallmentNumber, DueBillingPeriod = i.DueBillingPeriod,
                        Amount = i.Amount, PaymentStatus = (PaymentStatus)i.PaymentStatus,
                        SettledPaymentId = i.SettledPaymentId, PaymentReference = p == null ? null : p.PaymentReference,
                        ActivatedAt = i.ActivatedAt, ActivatedBillingReferenceId = i.ActivatedBillingReferenceId
                    }).ToListAsync();
            return otherCharges;
        }

        public async Task<OtherCharge> SaveUpdate(int userId, OtherCharge otherCharge)
        {
            if (userId <= 0) throw new InvalidOperationException("A valid user is required.");
            otherCharge.InstallmentCount = TMCWD.Model.Billing.OtherChargeInstallments.ParseCount(otherCharge.PayableIn);
            _ = TMCWD.Model.Billing.OtherChargeInstallments.BaseAmount(otherCharge.Amount, otherCharge.InstallmentCount.Value);
            if (otherCharge.Type <= 0 || !await _context.OtherFeeTypes.AnyAsync(t => t.Id == otherCharge.Type))
                throw new InvalidOperationException("Charge type is required.");
            if (string.IsNullOrWhiteSpace(otherCharge.BillingReferenceId))
                throw new InvalidOperationException("A billing record is required.");

            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            var original = otherCharge.Id > 0 ? await _context.OtherCharges.AsNoTracking().SingleOrDefaultAsync(c => c.Id == otherCharge.Id) : null;
            var bills = await LockBillings(new[] { original?.BillingReferenceId, otherCharge.BillingReferenceId });
            if (!bills.Any(b => b.BillingReferenceId == otherCharge.BillingReferenceId))
                throw new InvalidOperationException("The linked billing record was not found.");
            if (otherCharge.Id > 0)
            {
                var current = (await _context.OtherCharges.FromSqlInterpolated($"SELECT * FROM other_charges WHERE Id = {otherCharge.Id} FOR UPDATE")
                    .AsNoTracking().ToListAsync()).SingleOrDefault();
                if (current == null || current.BillingReferenceId != original?.BillingReferenceId)
                    throw new TMCWD.Model.Billing.PaymentConflictException();
                if (current.PaymentStatus != (int)PaymentStatus.Draft
                    || await _context.OtherChargeInstallments.AnyAsync(i => i.OtherChargeId == current.Id))
                    throw new InvalidOperationException("Only draft plans without installments can be edited.");
                otherCharge.CreatedBy = current.CreatedBy;
                otherCharge.DateCreated = current.DateCreated;
                _context.OtherCharges.Update(otherCharge);
            }
            else
            {
                otherCharge.CreatedBy = userId;
                otherCharge.DateCreated = DateTime.Now;
                _context.OtherCharges.Add(otherCharge);
            }
            otherCharge.PaymentStatus = (int)PaymentStatus.Draft;
            otherCharge.SettledPaymentId = null;
            otherCharge.UpdatedBy = userId;
            otherCharge.DateUpdated = DateTime.Now;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return otherCharge;
        }

        public async Task<int> PostToCollection(int userId, List<int> otherChargeIds)
        {
            if (userId <= 0)
                throw new InvalidOperationException("A valid user is required.");
            if (otherChargeIds == null || otherChargeIds.Count == 0 || otherChargeIds.Count > 1000)
                throw new InvalidOperationException("Select at least one draft charge to post.");

            var ids = otherChargeIds.Distinct().OrderBy(x => x).ToList();
            if (ids.Any(x => x <= 0))
                throw new InvalidOperationException("The selected charges are invalid.");

            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            var billings = await LockBillingsForCharges(ids);
            var charges = new List<OtherCharge>();
            foreach (var id in ids)
            {
                charges.AddRange(await _context.OtherCharges
                    .FromSqlInterpolated($"SELECT * FROM `other_charges` WHERE Id = {id} FOR UPDATE")
                    .ToListAsync());
            }

            if (charges.Count != ids.Count || charges.Any(x => !x.IsActive))
                throw new InvalidOperationException("One or more selected charges are no longer available.");
            if (charges.Any(x => x.PaymentStatus != (int)PaymentStatus.Draft
                              && x.PaymentStatus != (int)PaymentStatus.Unpaid
                              && x.PaymentStatus != (int)PaymentStatus.Overdue))
                throw new InvalidOperationException("One or more selected charges cannot be posted.");

            var references = charges.Select(x => x.BillingReferenceId)
                .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            if (references.Count == 0 || charges.Any(x => string.IsNullOrWhiteSpace(x.BillingReferenceId)))
                throw new InvalidOperationException("A selected charge is not attached to a billing record.");
            if (billings.Count != references.Count
                || references.Any(r => !billings.Any(b => b.BillingReferenceId == r)))
                throw new InvalidOperationException("The linked billing record for a selected charge was not found.");

            await ValidatePostedPlans(references);
            var now = DateTime.Now;
            foreach (var charge in charges.Where(x => x.PaymentStatus == (int)PaymentStatus.Draft))
            {
                var count = TMCWD.Model.Billing.OtherChargeInstallments.ParseCount(charge.PayableIn);
                if (charge.InstallmentCount != count) throw new InvalidOperationException("The installment count is inconsistent. Reload the draft.");
                var baseAmount = TMCWD.Model.Billing.OtherChargeInstallments.BaseAmount(charge.Amount, count);
                var anchor = billings.Single(b => b.BillingReferenceId == charge.BillingReferenceId);
                var authoritative = anchor.ReadingId > 0 && await _context.Readings.AnyAsync(r => r.Id == anchor.ReadingId
                    && r.AccountId == anchor.AccountId && r.Status == ReadingStatus.Completed);
                if (count > 1 && (!authoritative || anchor.BillingPeriod == default))
                    throw new InvalidOperationException("An installment charge requires an existing billing period.");
                if (anchor.BillingPeriod == default)
                    throw new InvalidOperationException("The linked billing record has no billing period.");
                var period = new DateTime(anchor.BillingPeriod.Year, anchor.BillingPeriod.Month, 1);
                if (count > (9999 - period.Year) * 12 + 13 - period.Month)
                    throw new InvalidOperationException("The installment schedule exceeds the supported billing periods.");
                if (await _context.OtherChargeInstallments.AnyAsync(i => i.OtherChargeId == charge.Id))
                    throw new TMCWD.Model.Billing.PaymentConflictException();
                for (var sequence = 1; sequence <= count; sequence++)
                    _context.OtherChargeInstallments.Add(new OtherChargeInstallment {
                        OtherChargeId = charge.Id, InstallmentNumber = sequence,
                        DueBillingPeriod = period.AddMonths(sequence - 1),
                        Amount = sequence == count ? charge.Amount - baseAmount * (count - 1) : baseAmount,
                        PaymentStatus = (int)(sequence == 1 ? PaymentStatus.Unpaid : PaymentStatus.Draft),
                        ActivatedBillingReferenceId = sequence == 1 ? anchor.BillingReferenceId : null,
                        ActivatedAt = sequence == 1 ? now : null,
                        CreatedBy = userId, UpdatedBy = userId, DateCreated = now, DateUpdated = now
                    });
                charge.PaymentStatus = (int)PaymentStatus.Unpaid;
                charge.UpdatedBy = userId;
                charge.DateUpdated = now;
            }
            await _context.SaveChangesAsync();

            // Posting the connection fee completes the Charging queue step.
            // Keep this in the same transaction as the ledger update so the
            // application cannot be shown in Payment without its charge.
            var accountIds = billings.Select(x => x.AccountId).Distinct().ToList();
            var applications = await _context.WaterConnectionApplications
                .Where(x => x.AccountId.HasValue
                    && accountIds.Contains(x.AccountId.Value)
                    && x.Stage == "charging")
                .ToListAsync();
            foreach (var application in applications)
            {
                var billing = billings.First(x => x.AccountId == application.AccountId);
                application.ChargingData = JsonSerializer.Serialize(new
                {
                    reference = billing.BillingReferenceId,
                    amount = (await GetCollectibleByReference(billing.BillingReferenceId)).Sum(c => c.Amount)
                });
                application.Stage = "payment";
                application.UpdatedAt = now;
            }
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return charges.Count;
        }

        public async Task<OtherCharge> Deactivate(int id, int userId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            var bills = await LockBillingsForCharges(new() { id });
            var otherCharge = (await _context.OtherCharges.FromSqlInterpolated($"SELECT * FROM `other_charges` WHERE Id = {id} FOR UPDATE").ToListAsync()).SingleOrDefault();
            if (otherCharge == null) return null;
            if (!string.IsNullOrWhiteSpace(otherCharge.BillingReferenceId) && !bills.Any(b => b.BillingReferenceId == otherCharge.BillingReferenceId))
                throw new TMCWD.Model.Billing.PaymentConflictException();
            if (otherCharge.PaymentStatus != (int)PaymentStatus.Draft)
                throw new InvalidOperationException("Posted charges cannot be removed from Collection.");

            otherCharge.IsActive = false;
            otherCharge.UpdatedBy = userId;
            otherCharge.DateUpdated = DateTime.Now;

            _context.OtherCharges.Update(otherCharge);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return otherCharge;
        }

        public async Task<string> ProcessPayment(
            int userId,
            TMCWD.Model.Billing.Requests.OtherChargePaymentRequest request)
        {
            if (userId <= 0)
                throw new InvalidOperationException("A valid cashier is required.");
            if (!Enum.IsDefined(typeof(PaymentMethod), request.Method))
                throw new InvalidOperationException("A valid payment method is required.");
            if (request.InstallmentIds == null || request.InstallmentIds.Count == 0 || request.InstallmentIds.Count > 1000)
                throw new InvalidOperationException("Select at least one installment to pay.");
            var installmentIds = request.InstallmentIds.Distinct().OrderBy(x => x).ToList();
            if (installmentIds.Any(id => id <= 0) || installmentIds.Count != request.InstallmentIds.Count)
                throw new InvalidOperationException("The selected installments are invalid or duplicated.");
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            var installmentParents = await _context.OtherChargeInstallments.AsNoTracking()
                .Where(i => installmentIds.Contains(i.Id)).Select(i => i.OtherChargeId).ToListAsync();
            var lockedBillings = await LockBillingsForCharges(installmentParents.Select(id => checked((int)id)).Distinct().ToList());

            var installments = new List<OtherChargeInstallment>();
            foreach (var id in installmentIds)
                installments.AddRange(await _context.OtherChargeInstallments
                    .FromSqlInterpolated($"SELECT * FROM `other_charge_installments` WHERE Id = {id} FOR UPDATE").ToListAsync());
            var parents = await _context.OtherCharges.Where(c => installmentParents.Contains(c.Id)).ToListAsync();
            if (installments.Count != installmentIds.Count || parents.Count != installmentParents.Distinct().Count() || parents.Any(c => !c.IsActive
                    || (c.PaymentStatus != (int)PaymentStatus.Unpaid && c.PaymentStatus != (int)PaymentStatus.Overdue))
                || installments.Any(i => i.PaymentStatus != (int)PaymentStatus.Unpaid && i.PaymentStatus != (int)PaymentStatus.Overdue)
                || parents.Any(c => !lockedBillings.Any(b => b.BillingReferenceId == c.BillingReferenceId)))
                throw new TMCWD.Model.Billing.PaymentConflictException();

            await ValidatePostedPlans(parents.Select(c => c.BillingReferenceId));
            var totalDue = installments.Sum(i => i.Amount);
            if (request.AmountTendered < totalDue)
                throw new InvalidOperationException("Amount tendered is less than the total due.");

            var now = DateTime.Now;
            var invoiceNumber = await PaymentReferenceAllocator.Allocate(_context, userId, request.Method, now, request.ExternalReference);
            var payments = new List<Payment>();

            foreach (var group in installments.GroupBy(i => parents.Single(c => c.Id == i.OtherChargeId).BillingReferenceId))
            {
                payments.Add(new Payment {
                    BillingReference = group.Key, PaymentReference = invoiceNumber, MacAddress = "WEB",
                    PaidAmount = group.Sum(i => i.Amount), ChargesOnly = true,
                    BalanceBefore = lockedBillings.Single(b => b.BillingReferenceId == group.Key).RemainingAmount,
                    Method = (int)request.Method, Success = true, CreatedBy = userId, DateCreated = now
                });
                foreach (var installment in group)
                {
                    installment.PaymentStatus = (int)PaymentStatus.Paid;
                    installment.UpdatedBy = userId;
                    installment.DateUpdated = now;
                }
            }

            _context.Payments.AddRange(payments);
            await _context.SaveChangesAsync();
            foreach (var installment in installments)
                installment.SettledPaymentId = payments.Single(p => p.BillingReference == parents.Single(c => c.Id == installment.OtherChargeId).BillingReferenceId).Id;
            await RefreshParents(installments.Select(i => i.OtherChargeId), userId);
            await _context.SaveChangesAsync();

            var oneTimeAccountIds = parents.Where(p => p.InstallmentCount == 1 && p.PaymentStatus == (int)PaymentStatus.Paid)
                .Select(p => lockedBillings.Single(b => b.BillingReferenceId == p.BillingReferenceId).AccountId);
            var settledAccountIds = oneTimeAccountIds.Distinct().ToList();
            if (settledAccountIds.Count > 0)
            {
                var applications = await _context.WaterConnectionApplications
                    .Where(x => x.AccountId.HasValue
                        && settledAccountIds.Contains(x.AccountId.Value)
                        && x.Stage == "payment")
                    .ToListAsync();
                foreach (var application in applications)
                {
                    application.PaymentData = JsonSerializer.Serialize(new
                    {
                        reference = invoiceNumber,
                        method = request.Method.ToString(),
                        date = now,
                        status = "Paid"
                    });
                    application.Stage = "installation";
                    application.UpdatedAt = now;
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return invoiceNumber;
        }

        private async Task<List<Billing>> LockBillingsForCharges(List<int> ids)
        {
            var references = await _context.OtherCharges.AsNoTracking().Where(c => ids.Contains((int)c.Id))
                .Select(c => c.BillingReferenceId).Distinct().ToListAsync();
            return await LockBillings(references);
        }

        public async Task<List<TMCWD.Model.Billing.CollectibleOtherCharge>> GetCollectibleByReference(string referenceId)
        {
            await ValidatePostedPlans(new[] { referenceId });
            var installments = await (from i in _context.OtherChargeInstallments.AsNoTracking()
                join c in _context.OtherCharges.AsNoTracking() on i.OtherChargeId equals c.Id
                where c.BillingReferenceId == referenceId && c.IsActive
                    && (c.PaymentStatus == (int)PaymentStatus.Unpaid || c.PaymentStatus == (int)PaymentStatus.Overdue)
                    && (i.PaymentStatus == (int)PaymentStatus.Unpaid || i.PaymentStatus == (int)PaymentStatus.Overdue)
                select new TMCWD.Model.Billing.CollectibleOtherCharge {
                    Id = (int)c.Id, InstallmentId = i.Id, InstallmentNumber = i.InstallmentNumber,
                    DueBillingPeriod = i.DueBillingPeriod, Type = c.Type, BillingReferenceId = c.BillingReferenceId,
                    PayableIn = c.PayableIn, Particulars = c.Particulars, Amount = i.Amount
                }).ToListAsync();
            return installments;
        }

        internal async Task RefreshParents(IEnumerable<long> parentIds, int userId)
        {
            foreach (var id in parentIds.Distinct())
            {
                var parent = await _context.OtherCharges.SingleAsync(c => c.Id == id);
                await ValidatePostedPlans(new[] { parent.BillingReferenceId });
                parent.PaymentStatus = await _context.OtherChargeInstallments.AnyAsync(i => i.OtherChargeId == id && i.PaymentStatus != (int)PaymentStatus.Paid)
                    ? (int)PaymentStatus.Unpaid : (int)PaymentStatus.Paid;
                parent.SettledPaymentId = null;
                parent.UpdatedBy = userId;
                parent.DateUpdated = DateTime.Now;
            }
        }

        internal async Task ActivateForBillings(List<Billing> billings, int userId)
        {
            var accountIds = billings.Select(b => b.AccountId).Distinct().ToList();
            var references = await (from c in _context.OtherCharges.AsNoTracking()
                join b in _context.Billings.AsNoTracking() on c.BillingReferenceId equals b.BillingReferenceId
                where accountIds.Contains(b.AccountId) && c.IsActive
                    && (c.PaymentStatus == (int)PaymentStatus.Unpaid || c.PaymentStatus == (int)PaymentStatus.Overdue)
                select c.BillingReferenceId).Distinct().ToListAsync();
            await LockBillings(references);
            await ValidatePostedPlans(references);
            foreach (var billing in billings.OrderBy(b => b.BillingPeriod))
            {
                var period = new DateTime(billing.BillingPeriod.Year, billing.BillingPeriod.Month, 1);
                var accountReferences = await _context.Billings.AsNoTracking().Where(b => b.AccountId == billing.AccountId
                    && references.Contains(b.BillingReferenceId)).Select(b => b.BillingReferenceId).ToListAsync();
                foreach (var reference in accountReferences.OrderBy(r => r, StringComparer.OrdinalIgnoreCase))
                {
                    var parents = await _context.OtherCharges.Where(c => c.BillingReferenceId == reference && c.IsActive
                        && (c.PaymentStatus == (int)PaymentStatus.Unpaid || c.PaymentStatus == (int)PaymentStatus.Overdue))
                        .Select(c => c.Id).ToListAsync();
                    var ids = await _context.OtherChargeInstallments.AsNoTracking().Where(i => parents.Contains(i.OtherChargeId)
                        && i.PaymentStatus == (int)PaymentStatus.Draft && i.DueBillingPeriod <= period)
                        .Select(i => i.Id).OrderBy(i => i).ToListAsync();
                    foreach (var id in ids)
                    {
                        var installment = (await _context.OtherChargeInstallments
                            .FromSqlInterpolated($"SELECT * FROM `other_charge_installments` WHERE Id = {id} FOR UPDATE").ToListAsync()).Single();
                        if (installment.PaymentStatus != (int)PaymentStatus.Draft) continue;
                        installment.PaymentStatus = (int)PaymentStatus.Unpaid;
                        installment.ActivatedBillingReferenceId = billing.BillingReferenceId;
                        installment.ActivatedAt = DateTime.Now;
                        installment.UpdatedBy = userId;
                        installment.DateUpdated = DateTime.Now;
                    }
                }
            }
        }

        internal async Task ValidatePostedPlans(IEnumerable<string> references)
        {
            var refs = references.Distinct().ToList();
            var ledgerTotals = _context.OtherChargeInstallments.AsNoTracking().GroupBy(i => i.OtherChargeId)
                .Select(g => new { ParentId = g.Key, Count = (int?)g.Count(), Amount = (decimal?)g.Sum(i => i.Amount) });
            var invalidId = await (from c in _context.OtherCharges.AsNoTracking()
                join totals in ledgerTotals on c.Id equals totals.ParentId into ledger
                from totals in ledger.DefaultIfEmpty()
                where refs.Contains(c.BillingReferenceId) && c.IsActive && c.PaymentStatus != (int)PaymentStatus.Draft
                    && (c.InstallmentCount == null || c.InstallmentCount < 1
                        || totals.Count == null || totals.Count != c.InstallmentCount || totals.Amount != c.Amount)
                select (long?)c.Id).FirstOrDefaultAsync();
            if (invalidId.HasValue)
                throw new InvalidOperationException($"Other Charge {invalidId.Value} has an invalid installment ledger. Contact the billing administrator.");
        }

        private async Task<List<Billing>> LockBillings(IEnumerable<string?> references)
        {
            var result = new List<Billing>();
            foreach (var reference in references.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct().OrderBy(r => r, StringComparer.OrdinalIgnoreCase))
                result.AddRange(await _context.Billings.FromSqlInterpolated($"SELECT * FROM `Billings` WHERE BillingReferenceId = {reference} FOR UPDATE").ToListAsync());
            return result;
        }
    }
}
