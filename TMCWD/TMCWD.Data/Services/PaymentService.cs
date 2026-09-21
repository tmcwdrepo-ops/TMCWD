using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class PaymentService : IPaymentService
    {

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

            var invoiceNumber = request.InvoiceNumber.Trim().ToUpperInvariant();
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

            var references = requestedBills.Select(x => x.BillingReferenceId).ToList();
            var billings = await _context.Billings
                .Where(x => references.Contains(x.BillingReferenceId))
                .ToListAsync();

            if (billings.Count != requestedBills.Count)
                throw new InvalidOperationException("One or more selected bills no longer exist.");
            if (billings.Any(x => x.PaymentStatus == (int)PaymentStatus.Paid ||
                                  x.PaymentStatus == (int)PaymentStatus.Waived))
                throw new InvalidOperationException("One or more selected bills have already been settled.");

            var totalToPay = requestedBills.Sum(x => x.PaidAmount);
            if (request.AmountTendered < totalToPay)
                throw new InvalidOperationException("Amount tendered is less than the selected bills.");

            var now = DateTime.Now;
            var payments = new List<Payment>();

            foreach (var billing in billings)
            {
                var billRequest = requestedBills.First(x =>
                    string.Equals(x.BillingReferenceId, billing.BillingReferenceId,
                        StringComparison.OrdinalIgnoreCase));

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

            _context.Payments.AddRange(payments);
            await _context.SaveChangesAsync();

            return payments;
        }

        #endregion

    }
}
