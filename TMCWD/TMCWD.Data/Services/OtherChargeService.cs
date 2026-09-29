using Microsoft.EntityFrameworkCore;
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

        public async Task<List<OtherCharge>> GetByReference(string referenceId)
        {
            var otherCharges = await _context.OtherCharges
                .Where(x => x.BillingReferenceId == referenceId && x.IsActive)
                .ToListAsync();
            return otherCharges;
        }

        public async Task<OtherCharge> SaveUpdate(int userId, OtherCharge otherCharge)
        {
            if(otherCharge.Id > 0)
            {
                otherCharge.UpdatedBy = userId;
                otherCharge.DateUpdated = DateTime.Now;
                _context.OtherCharges.Update(otherCharge);
            }
            else
            {
                otherCharge.CreatedBy = userId;
                otherCharge.DateCreated = DateTime.Now;
                _context.OtherCharges.Add(otherCharge);
            }

            await _context.SaveChangesAsync();
            return otherCharge;
        }

        public async Task<OtherCharge> Deactivate(int id, int userId)
        {
            var otherCharge = await _context.OtherCharges.Where(x => x.Id == id).FirstOrDefaultAsync();
            if (otherCharge == null) return null;

            otherCharge.IsActive = false;
            otherCharge.UpdatedBy = userId;
            otherCharge.DateUpdated = DateTime.Now;

            _context.OtherCharges.Update(otherCharge);
            await _context.SaveChangesAsync();
            return otherCharge;
        }

        public async Task<string> ProcessPayment(
            int userId,
            TMCWD.Model.Billing.Requests.OtherChargePaymentRequest request)
        {
            if (userId <= 0)
                throw new InvalidOperationException("A valid cashier is required.");
            if (string.IsNullOrWhiteSpace(request.InvoiceNumber))
                throw new InvalidOperationException("Invoice number is required.");
            if (!Enum.IsDefined(typeof(PaymentMethod), request.Method))
                throw new InvalidOperationException("A valid payment method is required.");
            if (request.OtherChargeIds == null || request.OtherChargeIds.Count == 0)
                throw new InvalidOperationException("Select at least one charge to pay.");

            var invoiceNumber = request.InvoiceNumber.Trim().ToUpperInvariant();
            if (await _context.Payments.AnyAsync(x => x.PaymentReference == invoiceNumber))
                throw new InvalidOperationException("This invoice number has already been used.");

            // Lock and load the charge rows atomically.
            var ids = request.OtherChargeIds.Distinct().OrderBy(x => x).ToList();
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var charges = new List<OtherCharge>();
            foreach (var id in ids)
            {
                var rows = await _context.OtherCharges
                    .FromSqlInterpolated($"SELECT * FROM `other_charges` WHERE Id = {id} FOR UPDATE")
                    .ToListAsync();
                charges.AddRange(rows);
            }

            if (charges.Count != ids.Count)
                throw new InvalidOperationException("One or more selected charges no longer exist.");

            var inactive = charges.Where(c => !c.IsActive).ToList();
            if (inactive.Count > 0)
                throw new InvalidOperationException("One or more selected charges are no longer active.");

            var alreadyPaid = charges.Where(c => c.PaymentStatus == (int)PaymentStatus.Paid).ToList();
            if (alreadyPaid.Count > 0)
                throw new InvalidOperationException("One or more selected charges have already been paid.");

            var totalDue = charges.Sum(c => c.Amount);
            if (request.AmountTendered < totalDue)
                throw new InvalidOperationException("Amount tendered is less than the total due.");

            var now = DateTime.Now;
            var payments = new List<Payment>();

            foreach (var charge in charges)
            {
                charge.PaymentStatus = (int)PaymentStatus.Paid;
                charge.UpdatedBy = userId;
                charge.DateUpdated = now;

                payments.Add(new Payment
                {
                    // Re-use BillingReference as the natural FK anchor;
                    // for standalone other-charges (no billing ref) we write a synthetic key.
                    BillingReference = string.IsNullOrWhiteSpace(charge.BillingReferenceId)
                        ? $"OC-{charge.Id}"
                        : charge.BillingReferenceId,
                    PaymentReference = invoiceNumber,
                    MacAddress = "WEB",
                    PaidAmount = charge.Amount,
                    Method = (int)request.Method,
                    Success = true,
                    CreatedBy = userId,
                    DateCreated = now
                });
            }

            _context.OtherCharges.UpdateRange(charges);
            _context.Payments.AddRange(payments);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return invoiceNumber;
        }
    }
}
