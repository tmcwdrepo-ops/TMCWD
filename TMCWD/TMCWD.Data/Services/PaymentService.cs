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
            if (string.IsNullOrWhiteSpace(request.InvoiceNumber))
                throw new InvalidOperationException("Invoice number is required.");
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

            var invoiceNumber = request.InvoiceNumber.Trim().ToUpperInvariant();
            await using var referenceLock = await PaymentReferenceLock.Acquire(_context, invoiceNumber);
            if (await _context.Payments.AnyAsync(x => x.PaymentReference == invoiceNumber))
                throw new InvalidOperationException("This invoice number has already been used.");

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
                .OrderBy(x => x, StringComparer.Ordinal).ToList();
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var billings = new List<Billing>();
            foreach (var reference in references)
                billings.AddRange(await _context.Billings
                    .FromSqlInterpolated($"SELECT * FROM `Billings` WHERE BillingReferenceId = {reference} FOR UPDATE")
                    .ToListAsync());

            if (billings.Count != requestedBills.Count)
                throw new InvalidOperationException("One or more selected bills no longer exist.");
            if (billings.Any(x => x.PaymentStatus == (int)PaymentStatus.Paid ||
                                  x.PaymentStatus == (int)PaymentStatus.Waived))
                throw new InvalidOperationException("One or more selected bills have already been settled.");

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

            var now = DateTime.Now;
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
                var outstandingOtherCharges = await _context.OtherCharges
                    .Where(c => c.BillingReferenceId == billing.BillingReferenceId && c.IsActive
                        && (c.PaymentStatus == (int)PaymentStatus.Unpaid
                         || c.PaymentStatus == (int)PaymentStatus.Overdue))
                    .SumAsync(c => (decimal?)c.Amount) ?? 0m;
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

                // Legacy new-connection stubs can have a zero RemainingAmount
                // even though a posted charge exists. Never collect less than
                // the active posted Other Charges balance.
                var collectibleBalance = Math.Max(0m, Math.Max(billing.RemainingAmount, outstandingOtherCharges));
                var amountDue = collectibleBalance - discountAmount + outstandingPenalty;
                if (billRequest.PaidAmount != amountDue)
                    throw new InvalidOperationException("The bill amount changed. Reload the account to include its current penalty before collecting payment.");

                payments.Add(new Payment
                {
                    BillingReference = billing.BillingReferenceId,
                    PaymentReference = invoiceNumber,
                    MacAddress = "WEB",
                    PaidAmount = billRequest.PaidAmount,
                    Method = (int)request.Method,
                    Success = true,
                    CreatedBy = userId,
                    DateCreated = now
                });

                billing.PaymentStatus = (int)PaymentStatus.Paid;
                billing.PaymentTransactionId = invoiceNumber;
                billing.BillingAdjustment += discountAmount;
                billing.RemainingAmount = 0;
                billing.UpdatedBy = userId;
                billing.DateUpdated = now;
            }

            var penalties = await _context.Penalties
                .Where(x => references.Contains(x.BillingReferenceId) &&
                            x.PaymentStatus != (int)PaymentStatus.Waived)
                .ToListAsync();
            foreach (var penalty in penalties)
            {
                penalty.PaymentStatus = (int)PaymentStatus.Paid;
                penalty.UpdatedBy = userId;
                penalty.DateUpdated = now;
            }

            var otherCharges = await _context.OtherCharges
                .Where(x => references.Contains(x.BillingReferenceId) && x.IsActive
                    && (x.PaymentStatus == (int)PaymentStatus.Unpaid
                     || x.PaymentStatus == (int)PaymentStatus.Overdue))
                .ToListAsync();
            foreach (var charge in otherCharges)
            {
                charge.PaymentStatus = (int)PaymentStatus.Paid;
                charge.UpdatedBy = userId;
                charge.DateUpdated = now;
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
            // of how the user typed it — invoice numbers are stored uppercase.
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

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var payments = await _context.Payments
                .FromSqlInterpolated($"SELECT * FROM `payments` WHERE `PaymentReference` = {invoiceNumber} FOR UPDATE")
                .ToListAsync();
            if (payments.Count == 0)
                throw new InvalidOperationException("Invoice was not found.");
            if (payments.Any(x => !x.Success)
                || await _context.InvoiceCancellations.AnyAsync(x => x.PaymentReference == invoiceNumber))
                throw new InvalidOperationException("This invoice has already been cancelled.");

            var now = DateTime.Now;
            foreach (var payment in payments)
            {
                var billing = (await _context.Billings
                    .FromSqlInterpolated($"SELECT * FROM `Billings` WHERE `BillingReferenceId` = {payment.BillingReference} FOR UPDATE")
                    .ToListAsync()).SingleOrDefault()
                    ?? throw new InvalidOperationException(
                        $"Billing record {payment.BillingReference} could not be restored.");

                var paidPenalties = await _context.Penalties
                    .Where(x => x.BillingReferenceId == payment.BillingReference
                        && x.PaymentStatus == (int)PaymentStatus.Paid
                        && x.UpdatedBy == payment.CreatedBy
                        && x.DateUpdated == payment.DateCreated)
                    .ToListAsync();
                var penaltyAmount = paidPenalties.Sum(x => x.Amount);
                var paidOtherCharges = await _context.OtherCharges
                    .Where(x => x.BillingReferenceId == payment.BillingReference
                        && x.PaymentStatus == (int)PaymentStatus.Paid
                        && x.UpdatedBy == payment.CreatedBy
                        && x.DateUpdated == payment.DateCreated)
                    .ToListAsync();
                var discounts = await _context.SeniorCitizenDiscounts
                    .Where(x => x.BillingReferenceId == payment.BillingReference && !x.IsCancelled)
                    .ToListAsync();
                var discountAmount = discounts.Sum(x => x.DiscountAmount);

                billing.RemainingAmount = payment.PaidAmount - penaltyAmount + discountAmount;
                billing.BillingAdjustment = Math.Max(0m, billing.BillingAdjustment - discountAmount);
                billing.PaymentStatus = (int)PaymentStatus.Unpaid;
                billing.PaymentTransactionId = string.Empty;
                billing.UpdatedBy = userId;
                billing.DateUpdated = now;

                foreach (var penalty in paidPenalties)
                {
                    penalty.PaymentStatus = (int)PaymentStatus.Unpaid;
                    penalty.UpdatedBy = userId;
                    penalty.DateUpdated = now;
                }

                foreach (var charge in paidOtherCharges)
                {
                    charge.PaymentStatus = (int)PaymentStatus.Unpaid;
                    charge.UpdatedBy = userId;
                    charge.DateUpdated = now;
                }

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
