using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class PaymentService : IPaymentService
    {
        private const decimal SeniorCitizenDiscountRate = 0.20m;

        #region fields

        private readonly UserDbContext _context;

        #endregion

        #region constructors

        public PaymentService(UserDbContext context)
        {
            _context = context;
        }

        #endregion

        #region methods

        public async Task<Payment> Get(int id)
        {
            var payment = await _context.Payments.Where(x => x.Id == id).FirstOrDefaultAsync();
            return payment;
        }

        public async Task<List<Payment>> GetByBillingReference(string referenceId)
        {
            var payments = await _context.Payments.Where(x => x.BillingReference.ToLower() == referenceId.ToLower()).ToListAsync();
            return payments;
        }

        public async Task<List<Payment>> GetByCashier(int userId)
        {
            var payments = await _context.Payments.Where(x => x.CreatedBy == userId).ToListAsync();
            return payments;
        }

        public async Task<List<Payment>> GetByMethod(int methodId)
        {
            var payments = await _context.Payments.Where(x => x.Method ==  methodId).ToListAsync();
            return payments;
        }

        public async Task<List<Payment>> GetByMacAddress(string macAddress)
        {
            var payments = await _context.Payments.Where(x => x.MacAddress.ToLower() == macAddress.ToLower()).ToListAsync();
            return payments;
        }

        public async Task<Payment> GetByPaymentReference(string referenceId)
        {
            var payment = await _context.Payments.Where(x=>x.PaymentReference.ToLower() == referenceId.ToLower()).FirstOrDefaultAsync();
            return payment;
        }

        public async Task<Payment> SaveUpdate(int userId, Payment payment)
        {
            if (payment.Id == 0)
            {
                payment.CreatedBy = userId;
                payment.DateCreated = DateTime.Now;
                _context.Payments.Add(payment);
            }
            else
            {
                payment.UpdatedBy = userId;
                payment.DateUpdated = DateTime.Now;
                _context.Payments.Update(payment);
            }

            await _context.SaveChangesAsync();

            return payment;

        }

        public async Task<List<Payment>> ProcessCollectionPayment(
            int userId,
            TMCWD.Model.Billing.Requests.CollectionPaymentRequest request)
        {
            if (userId <= 0)
                throw new InvalidOperationException("A valid cashier is required.");
            if (!Enum.IsDefined(typeof(PaymentMethod), request.Method))
                throw new InvalidOperationException("A valid payment method is required.");
            if (request.Bills == null || request.Bills.Count == 0)
                throw new InvalidOperationException("Select at least one bill.");

            var seniorDiscount = request.SeniorCitizenDiscount;
            if (seniorDiscount != null)
            {
                seniorDiscount.IdNumber = seniorDiscount.IdNumber?.Trim() ?? string.Empty;
                seniorDiscount.Name = seniorDiscount.Name?.Trim() ?? string.Empty;
                if (seniorDiscount.IdNumber.Length < 3 || seniorDiscount.IdNumber.Length > 30)
                    throw new InvalidOperationException("A valid Senior Citizen ID number is required.");
                if (seniorDiscount.Name.Length < 3 || seniorDiscount.Name.Length > 160)
                    throw new InvalidOperationException("The Senior Citizen name is required.");
            }

            var requestedBills = request.Bills
                .Where(x => !string.IsNullOrWhiteSpace(x.BillingReferenceId))
                .GroupBy(x => x.BillingReferenceId.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .ToList();

            if (requestedBills.Count != request.Bills.Count)
                throw new InvalidOperationException("The payment contains duplicate or invalid bills.");
            if (requestedBills.Any(x => x.PaidAmount <= 0))
                throw new InvalidOperationException("Every selected bill must have a valid payment amount.");

            var references = requestedBills.Select(x => x.BillingReferenceId.Trim())
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            var now = DateTime.Now;
            var billings = new List<Billing>();
            foreach (var reference in references)
                billings.AddRange(await _context.Billings
                    .FromSqlInterpolated($"SELECT * FROM `Billings` WHERE BillingReferenceId = {reference} FOR UPDATE")
                    .ToListAsync());

            if (billings.Count != requestedBills.Count)
                throw new InvalidOperationException("One or more selected bills no longer exist.");
            await new OtherChargeService(_context).ValidatePostedPlans(references);
            var installmentPayments = new List<OtherChargeInstallment>();
            foreach (var reference in references)
                installmentPayments.AddRange(await _context.OtherChargeInstallments.FromSqlInterpolated($@"
                    SELECT i.* FROM other_charge_installments i
                    INNER JOIN other_charges c ON c.Id = i.OtherChargeId
                    WHERE c.BillingReferenceId = {reference} AND c.IsActive = 1
                      AND c.PaymentStatus IN (1,3) AND i.PaymentStatus IN (1,3)
                    ORDER BY i.Id FOR UPDATE").ToListAsync());
            var installmentParents = await _context.OtherCharges
                .Where(c => references.Contains(c.BillingReferenceId)
                    && _context.OtherChargeInstallments.Any(i => i.OtherChargeId == c.Id)).ToListAsync();
            foreach (var billRequest in requestedBills)
            {
                var expected = installmentPayments.Where(i => installmentParents.Any(c => c.Id == i.OtherChargeId
                    && c.BillingReferenceId == billRequest.BillingReferenceId.Trim())).Select(i => i.Id).OrderBy(id => id).ToList();
                if (billRequest.InstallmentIds == null || !billRequest.InstallmentIds.OrderBy(id => id).SequenceEqual(expected))
                    throw new TMCWD.Model.Billing.PaymentConflictException();
            }
            if (billings.Any(b => (b.PaymentStatus == (int)PaymentStatus.Paid || b.PaymentStatus == (int)PaymentStatus.Waived)
                && !installmentPayments.Any(i => installmentParents.Any(c => c.Id == i.OtherChargeId && c.BillingReferenceId == b.BillingReferenceId))))
                throw new TMCWD.Model.Billing.PaymentConflictException();

            Dictionary<long, Account> accounts = new();
            Dictionary<long, Customer> customers = new();
            if (seniorDiscount != null)
            {
                var accountIds = billings.Select(x => x.AccountId).Distinct().ToList();
                if (accountIds.Count != 1)
                    throw new InvalidOperationException(
                        "A Senior Citizen discount can only be applied to bills from one account.");

                accounts = await _context.Accounts
                    .Where(x => accountIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id);
                var customerIds = accounts.Values.Select(x => x.CustomerId).Distinct().ToList();
                customers = await _context.Customers
                    .Where(x => customerIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id);

                if (accounts.Count != accountIds.Count || customers.Count != customerIds.Count)
                    throw new InvalidOperationException("The account holder could not be verified for this discount.");
                if (await _context.SeniorCitizenDiscounts.AnyAsync(x =>
                    references.Contains(x.BillingReferenceId) && !x.IsCancelled))
                    throw new InvalidOperationException("A Senior Citizen discount has already been applied to one or more selected bills.");
            }

            var totalToPay = requestedBills.Sum(x => x.PaidAmount);
            if (request.AmountTendered < totalToPay)
                throw new InvalidOperationException("Amount tendered is less than the selected bills.");

            var invoiceNumber = await PaymentReferenceAllocator.Allocate(_context, userId, request.Method, now, request.ExternalReference);
            var payments = new List<Payment>();
            var discountAdjustments = new List<BillingAdjustment>();
            var discountRecords = new List<SeniorCitizenDiscount>();

            foreach (var billing in billings)
            {
                var billRequest = requestedBills.First(x =>
                    string.Equals(x.BillingReferenceId.Trim(), billing.BillingReferenceId,
                        StringComparison.OrdinalIgnoreCase));

                var outstandingPenalty = await _context.Penalties
                    .Where(p => p.BillingReferenceId == billing.BillingReferenceId
                        && (p.PaymentStatus == (int)PaymentStatus.Unpaid || p.PaymentStatus == (int)PaymentStatus.Overdue))
                    .SumAsync(p => p.Amount);
                var discountAmount = 0m;
                if (seniorDiscount != null)
                {
                    var account = accounts[billing.AccountId];
                    var customer = customers[account.CustomerId];
                    var accountHolderName = BuildCustomerName(customer);
                    if (!string.Equals(NormalizePersonName(seniorDiscount.Name),
                        NormalizePersonName(accountHolderName), StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            $"The Senior Citizen name must exactly match the account holder: {accountHolderName}.");

                    var invoice = billing.InvoiceSnapshotJson == null ? null
                        : JsonSerializer.Deserialize<TMCWD.Model.Billing.OfficialInvoiceSnapshot>(billing.InvoiceSnapshotJson);
                    if (invoice == null)
                        throw new InvalidOperationException("The bill has no authoritative water-charge snapshot for this discount.");
                    var waterCharge = invoice.BillAmount;
                    if (waterCharge <= 0m)
                        throw new InvalidOperationException("The selected bill has no water-consumption charge eligible for discount.");

                    discountAmount = Math.Round(waterCharge * SeniorCitizenDiscountRate, 2,
                        MidpointRounding.AwayFromZero);
                    discountAdjustments.Add(new BillingAdjustment
                    {
                        BillingReferenceId = billing.BillingReferenceId,
                        Type = (int)AdjustmentLineType.SeniorCitizenDiscount,
                        Amount = discountAmount,
                        CreatedBy = userId,
                        DateCreated = now
                    });
                    discountRecords.Add(new SeniorCitizenDiscount
                    {
                        BillingReferenceId = billing.BillingReferenceId,
                        SeniorCitizenIdNumber = seniorDiscount.IdNumber,
                        SeniorCitizenName = accountHolderName,
                        AccountHolderName = accountHolderName,
                        Rate = SeniorCitizenDiscountRate,
                        WaterCharge = waterCharge,
                        DiscountAmount = discountAmount,
                        CreatedBy = userId,
                        DateCreated = now
                    });
                }

                var alreadySettled = billing.PaymentStatus == (int)PaymentStatus.Paid || billing.PaymentStatus == (int)PaymentStatus.Waived;
                var collectibleBalance = alreadySettled ? 0m : Math.Max(0m, billing.RemainingAmount);
                var installmentAmount = installmentPayments.Where(i => installmentParents.Any(c => c.Id == i.OtherChargeId
                    && c.BillingReferenceId == billing.BillingReferenceId)).Sum(i => i.Amount);
                if (alreadySettled && seniorDiscount != null)
                    throw new InvalidOperationException("A settled water bill cannot receive another discount.");
                var amountDue = collectibleBalance - discountAmount + (alreadySettled ? 0m : outstandingPenalty) + installmentAmount;
                if (billRequest.PaidAmount != amountDue)
                    throw new TMCWD.Model.Billing.PaymentConflictException();

                payments.Add(new Payment
                {
                    BillingReference = billing.BillingReferenceId,
                    PaymentReference = invoiceNumber,
                    MacAddress = "WEB",
                    PaidAmount = billRequest.PaidAmount,
                    BalanceBefore = collectibleBalance,
                    ChargesOnly = alreadySettled,
                    Method = (int)request.Method,
                    Success = true,
                    CreatedBy = userId,
                    DateCreated = now
                });

                if (alreadySettled) continue;
                billing.PaymentStatus = (int)PaymentStatus.Paid;
                billing.PaymentTransactionId = invoiceNumber;
                billing.BillingAdjustment += discountAmount;
                billing.RemainingAmount = 0;
                billing.UpdatedBy = userId;
                billing.DateUpdated = now;
            }

            var waterReferences = payments.Where(p => !p.ChargesOnly).Select(p => p.BillingReference).ToList();
            var penalties = await _context.Penalties
                .Where(x => waterReferences.Contains(x.BillingReferenceId) &&
                            (x.PaymentStatus == (int)PaymentStatus.Unpaid || x.PaymentStatus == (int)PaymentStatus.Overdue))
                .ToListAsync();
            foreach (var penalty in penalties)
            {
                penalty.PaymentStatus = (int)PaymentStatus.Paid;
                penalty.UpdatedBy = userId;
                penalty.DateUpdated = now;
            }

            foreach (var installment in installmentPayments)
            {
                installment.PaymentStatus = (int)PaymentStatus.Paid;
                installment.UpdatedBy = userId;
                installment.DateUpdated = now;
            }

            var paidAccountIds = billings.Select(x => x.AccountId).Distinct().ToList();
            var paidApplications = await _context.WaterConnectionApplications
                .Where(x => x.AccountId.HasValue
                    && paidAccountIds.Contains(x.AccountId.Value)
                    && x.Stage == "payment")
                .ToListAsync();
            foreach (var application in paidApplications)
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

            _context.Payments.AddRange(payments);
            _context.BillingAdjustments.AddRange(discountAdjustments);
            _context.SeniorCitizenDiscounts.AddRange(discountRecords);
            await _context.SaveChangesAsync();
            foreach (var penalty in penalties)
                penalty.SettledPaymentId = payments.Single(p => p.BillingReference == penalty.BillingReferenceId).Id;
            foreach (var installment in installmentPayments)
                installment.SettledPaymentId = payments.Single(p => p.BillingReference == installmentParents.Single(c => c.Id == installment.OtherChargeId).BillingReferenceId).Id;
            await new OtherChargeService(_context).RefreshParents(installmentPayments.Select(i => i.OtherChargeId), userId);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return payments;
        }

        public async Task<List<TMCWD.Model.Billing.InvoiceRecord>> SearchInvoices(string query, int limit = 50)
        {
            query = (query ?? string.Empty).Trim();
            if (query.Length == 0)
                return new();

            limit = Math.Clamp(limit, 1, 100);

            // Normalise to uppercase so the comparison is consistent regardless
            // of how the user typed it � invoice numbers are stored uppercase.
            var upper = query.ToUpperInvariant();

            var references = await _context.Payments.AsNoTracking()
                .Where(x => x.PaymentReference.ToUpper().Contains(upper))
                .OrderByDescending(x => x.DateCreated)
                .Select(x => x.PaymentReference)
                .Distinct()
                .Take(limit)
                .ToListAsync();

            // If nothing matched by OR number, try matching by billing reference
            // (account number / reference ID) so cashiers can search either way.
            if (references.Count == 0)
            {
                references = await _context.Payments.AsNoTracking()
                    .Where(x => x.BillingReference.ToUpper().Contains(upper))
                    .OrderByDescending(x => x.DateCreated)
                    .Select(x => x.PaymentReference)
                    .Distinct()
                    .Take(limit)
                    .ToListAsync();
            }

            return await LoadInvoiceRecords(references);
        }

        public async Task<TMCWD.Model.Billing.InvoiceRecord?> GetInvoice(string invoiceNumber)
        {
            invoiceNumber = (invoiceNumber ?? string.Empty).Trim();
            if (invoiceNumber.Length == 0)
                return null;
            return (await LoadInvoiceRecords(new List<string> { invoiceNumber }))
                .FirstOrDefault(x => string.Equals(x.InvoiceNo, invoiceNumber,
                    StringComparison.OrdinalIgnoreCase));
        }

        public async Task CancelInvoice(
            int userId,
            TMCWD.Model.Billing.CancelInvoiceRequest request)
        {
            if (userId <= 0)
                throw new InvalidOperationException("A valid cashier is required.");
            var invoiceNumber = request.InvoiceNo?.Trim() ?? string.Empty;
            var reason = request.Reason?.Trim() ?? string.Empty;
            if (invoiceNumber.Length == 0)
                throw new InvalidOperationException("Invoice number is required.");
            if (reason.Length < 3 || reason.Length > 200)
                throw new InvalidOperationException("A cancellation reason between 3 and 200 characters is required.");

            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            var preview = await _context.Payments.AsNoTracking().Where(p => p.PaymentReference == invoiceNumber).ToListAsync();
            if (preview.Count == 0) throw new InvalidOperationException("Payment receipt was not found.");
            if (preview.Any(p => p.CreatedBy != userId) && !await _context.Users.AnyAsync(u => u.Id == userId && u.Role == (int)UserRole.SuperAdmin))
                throw new UnauthorizedAccessException("Only the collecting cashier or SuperAdmin can cancel this payment.");
            var lockedBills = new List<Billing>();
            foreach (var reference in preview.Select(p => p.BillingReference).Distinct().OrderBy(r => r, StringComparer.OrdinalIgnoreCase))
                lockedBills.AddRange(await _context.Billings.FromSqlInterpolated($"SELECT * FROM `Billings` WHERE BillingReferenceId = {reference} FOR UPDATE").ToListAsync());
            var payments = await _context.Payments
                .FromSqlInterpolated($"SELECT * FROM `payments` WHERE `PaymentReference` = {invoiceNumber} FOR UPDATE")
                .ToListAsync();
            if (payments.Count == 0)
                throw new InvalidOperationException("Invoice was not found.");
            if (payments.Any(x => !x.Success)
                || await _context.InvoiceCancellations.AnyAsync(x => x.PaymentReference == invoiceNumber))
                throw new InvalidOperationException("This invoice has already been cancelled.");
            await new OtherChargeService(_context).ValidatePostedPlans(payments.Select(p => p.BillingReference));

            var now = DateTime.Now;
            foreach (var payment in payments)
            {
                var billing = lockedBills.SingleOrDefault(b => b.BillingReferenceId == payment.BillingReference);
                if ((!payment.ChargesOnly && (billing == null || payment.BalanceBefore == null
                        || billing.PaymentStatus != (int)PaymentStatus.Paid || billing.PaymentTransactionId != invoiceNumber))
                    || await _context.Payments.AnyAsync(p => p.Success && p.BillingReference == payment.BillingReference && p.Id > payment.Id))
                    throw new TMCWD.Model.Billing.PaymentConflictException();

                var paidPenalties = await _context.Penalties
                    .Where(x => x.BillingReferenceId == payment.BillingReference
                        && x.PaymentStatus == (int)PaymentStatus.Paid
                        && x.SettledPaymentId == payment.Id)
                    .ToListAsync();
                var paidInstallments = await _context.OtherChargeInstallments
                    .FromSqlInterpolated($"SELECT * FROM other_charge_installments WHERE SettledPaymentId = {payment.Id} AND PaymentStatus = 2 ORDER BY Id FOR UPDATE")
                    .ToListAsync();
                if (payment.ChargesOnly && (paidInstallments.Count == 0 || paidInstallments.Sum(i => i.Amount) != payment.PaidAmount))
                    throw new InvalidOperationException("This receipt has an invalid installment settlement. Contact the billing administrator.");
                var discounts = await _context.SeniorCitizenDiscounts
                    .Where(x => !payment.ChargesOnly && x.BillingReferenceId == payment.BillingReference && !x.IsCancelled)
                    .ToListAsync();
                var discountAmount = discounts.Sum(x => x.DiscountAmount);

                if (billing != null && !payment.ChargesOnly) {
                    billing.RemainingAmount = payment.BalanceBefore ?? throw new InvalidOperationException("Historical receipt requires manual balance review before cancellation.");
                    billing.BillingAdjustment = Math.Max(0m, billing.BillingAdjustment - discountAmount);
                    billing.PaymentStatus = (int)PaymentStatus.Unpaid;
                    billing.PaymentTransactionId = string.Empty;
                    billing.UpdatedBy = userId;
                    billing.DateUpdated = now;
                }

                foreach (var penalty in paidPenalties)
                {
                    penalty.PaymentStatus = (int)PaymentStatus.Unpaid;
                    penalty.SettledPaymentId = null;
                    penalty.UpdatedBy = userId;
                    penalty.DateUpdated = now;
                }

                foreach (var installment in paidInstallments)
                {
                    installment.PaymentStatus = (int)PaymentStatus.Unpaid;
                    installment.SettledPaymentId = null;
                    installment.UpdatedBy = userId;
                    installment.DateUpdated = now;
                }
                await _context.SaveChangesAsync();
                await new OtherChargeService(_context).RefreshParents(paidInstallments.Select(i => i.OtherChargeId), userId);

                foreach (var discount in discounts)
                {
                    discount.IsCancelled = true;
                    discount.CancelledBy = userId;
                    discount.DateCancelled = now;
                    _context.BillingAdjustments.Add(new BillingAdjustment
                    {
                        BillingReferenceId = payment.BillingReference,
                        Type = (int)AdjustmentLineType.SeniorCitizenDiscountReversal,
                        Amount = -discount.DiscountAmount,
                        CreatedBy = userId,
                        DateCreated = now
                    });
                }

                payment.Success = false;
                payment.UpdatedBy = userId;
                payment.DateUpdated = now;
            }

            _context.InvoiceCancellations.Add(new InvoiceCancellation
            {
                PaymentReference = invoiceNumber,
                Reason = reason,
                CancelledBy = userId,
                DateCancelled = now
            });
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        private async Task<List<TMCWD.Model.Billing.InvoiceRecord>> LoadInvoiceRecords(
            IReadOnlyCollection<string> invoiceNumbers)
        {
            if (invoiceNumbers.Count == 0)
                return new();

            var payments = await _context.Payments.AsNoTracking()
                .Where(x => invoiceNumbers.Contains(x.PaymentReference))
                .OrderBy(x => x.DateCreated)
                .ToListAsync();
            var billingReferences = payments.Select(x => x.BillingReference).Distinct().ToList();
            var billings = await _context.Billings.AsNoTracking()
                .Where(x => billingReferences.Contains(x.BillingReferenceId))
                .ToDictionaryAsync(x => x.BillingReferenceId);
            var accountIds = billings.Values.Select(x => x.AccountId).Distinct().ToList();
            var accounts = await _context.Accounts.AsNoTracking()
                .Where(x => accountIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
            var customerIds = accounts.Values.Select(x => x.CustomerId).Distinct().ToList();
            var customers = await _context.Customers.AsNoTracking()
                .Where(x => customerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
            var cancellations = await _context.InvoiceCancellations.AsNoTracking()
                .Where(x => invoiceNumbers.Contains(x.PaymentReference))
                .ToDictionaryAsync(x => x.PaymentReference);
            var cashiers = await _context.Users.AsNoTracking().Where(u => payments.Select(p => p.CreatedBy).Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name);
            var receiptHeaders = await _context.PaymentReceipts.AsNoTracking().Where(r => invoiceNumbers.Contains(r.PaymentReference!))
                .ToDictionaryAsync(r => r.PaymentReference!);

            return payments.GroupBy(x => x.PaymentReference, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var first = group.First();
                    cancellations.TryGetValue(group.Key, out var cancellation);
                    var lines = group.Select(payment =>
                    {
                        billings.TryGetValue(payment.BillingReference, out var billing);
                        var account = billing != null && accounts.TryGetValue(billing.AccountId, out var foundAccount)
                            ? foundAccount : null;
                        var customer = account != null && customers.TryGetValue(account.CustomerId, out var foundCustomer)
                            ? foundCustomer : null;
                        return new TMCWD.Model.Billing.InvoiceLineRecord
                        {
                            BillingReference = payment.BillingReference,
                            AccountNumber = account?.AccountNumber ?? string.Empty,
                            AccountHolder = customer == null ? string.Empty : BuildCustomerName(customer),
                            Amount = payment.PaidAmount
                        };
                    }).ToList();
                    var accountsLabel = string.Join(", ", lines.Select(x =>
                        string.IsNullOrWhiteSpace(x.AccountHolder) ? x.AccountNumber : x.AccountHolder)
                        .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
                    return new TMCWD.Model.Billing.InvoiceRecord
                    {
                        InvoiceNo = group.Key,
                        Account = accountsLabel,
                        TotalAmount = group.Sum(x => x.PaidAmount),
                        TransactionType = Enum.IsDefined(typeof(PaymentMethod), first.Method)
                            ? ((PaymentMethod)first.Method).ToString() : "Unknown",
                        DateIssued = first.DateCreated,
                        CashierId = first.CreatedBy,
                        CashierName = cashiers.GetValueOrDefault(first.CreatedBy) ?? $"User #{first.CreatedBy}",
                        ExternalReference = receiptHeaders.GetValueOrDefault(group.Key)?.ExternalReference,
                        Status = group.All(x => x.Success) ? "Paid" : "Cancelled",
                        CancellationReason = cancellation?.Reason ?? string.Empty,
                        Lines = lines
                    };
                })
                .OrderByDescending(x => x.DateIssued)
                .ToList();
        }

        private static string BuildCustomerName(Customer customer)
        {
            var givenNames = string.Join(" ", new[] { customer.Firstname, customer.Middlename }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()));
            return $"{customer.Lastname?.Trim()}, {givenNames}".Trim();
        }

        private static string NormalizePersonName(string value)
        {
            var words = (value ?? string.Empty)
                .Normalize(NormalizationForm.FormKC)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return string.Join(' ', words).ToUpperInvariant();
        }

        public async Task<List<Payment>> GetByDateRange(DateTime from, DateTime to, int cashierId = 0)
        {
            var query = _context.Payments.AsNoTracking()
                .Where(p => p.Success
                    && p.DateCreated.Date >= from.Date
                    && p.DateCreated.Date <= to.Date);

            if (cashierId > 0)
                query = query.Where(p => p.CreatedBy == cashierId);

            return await query.OrderBy(p => p.PaymentReference).ToListAsync();
        }

        #endregion

    }
}
