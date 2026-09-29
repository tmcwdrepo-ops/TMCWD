using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class BillingAdjustmentService : IBillingAdjustmentService
    {
        private readonly UserDbContext _context;

        public BillingAdjustmentService(UserDbContext context)
        {
            _context = context;
        }

        public async Task<BillingAdjustment> Get(int id)
        {
            var billingAdjustment = await _context.BillingAdjustments.Where(x => x.Id == id).FirstOrDefaultAsync();
            return billingAdjustment;
        }

        public async Task<List<BillingAdjustment>> GetAll()
        {
            var billingAdjustments = await _context.BillingAdjustments.ToListAsync();
            return billingAdjustments;
        }

        public async Task<List<BillingAdjustment>> GetByReference(string reference)
        {
            var billingAdjustments = await _context.BillingAdjustments.Where(x => x.BillingReferenceId == reference).ToListAsync();
            return billingAdjustments;
        }

        public async Task<BillingAdjustment> SaveUpdate(int userId, BillingAdjustment billingAdjustment)
        {
            if (billingAdjustment.Id == 0)
            {
                billingAdjustment.CreatedBy = userId;
                billingAdjustment.DateCreated = DateTime.UtcNow;
                _context.BillingAdjustments.Add(billingAdjustment);
            }
            else
            {
                var existingBillingAdjustment = await _context.BillingAdjustments.FindAsync(billingAdjustment.Id);

                if (existingBillingAdjustment != null)
                {
                    existingBillingAdjustment.Type = billingAdjustment.Type;
                    existingBillingAdjustment.Amount = billingAdjustment.Amount;
                    existingBillingAdjustment.UpdatedBy = userId;
                    existingBillingAdjustment.DateUpdated = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            var billing = await _context.Billings
                .FirstOrDefaultAsync(x => x.BillingReferenceId == billingAdjustment.BillingReferenceId);

            if (billing != null)
            {
                billing.BillingAdjustment = await _context.BillingAdjustments
                    .Where(x => x.BillingReferenceId == billingAdjustment.BillingReferenceId)
                    .SumAsync(x => x.Amount);
                billing.RemainingAmount = Math.Max(TMCWD.Model.Billing.BillingCharges.MeterMaintenanceFee,
                    billing.TotalBillAmount - billing.BillingAdjustment);
                billing.UpdatedBy = userId;
                billing.DateUpdated = DateTime.Now;

                await _context.SaveChangesAsync();
            }

            return billingAdjustment;
        }

        public async Task<List<BillingAdjustment>> SaveBatch(
            int userId,
            List<BillingAdjustment> billingAdjustments)
        {
            if (userId <= 0 || billingAdjustments == null || billingAdjustments.Count == 0)
                throw new InvalidOperationException("At least one valid adjustment is required.");

            var reference = billingAdjustments[0].BillingReferenceId?.Trim();
            if (string.IsNullOrWhiteSpace(reference)
                || billingAdjustments.Any(item =>
                    !string.Equals(item.BillingReferenceId?.Trim(), reference,
                        StringComparison.OrdinalIgnoreCase)
                    || item.Amount == 0))
                throw new InvalidOperationException("All adjustments must belong to the same bill and have a non-zero amount.");

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var billing = await _context.Billings
                .FromSqlInterpolated($"SELECT * FROM `Billings` WHERE BillingReferenceId = {reference} FOR UPDATE")
                .SingleOrDefaultAsync();
            if (billing == null)
                throw new InvalidOperationException("The selected billing record no longer exists.");
            if (billing.PaymentStatus == (int)PaymentStatus.Paid
                || billing.PaymentStatus == (int)PaymentStatus.Waived)
                throw new InvalidOperationException("A settled bill cannot be adjusted.");

            var now = DateTime.UtcNow;
            foreach (var adjustment in billingAdjustments)
            {
                adjustment.BillingReferenceId = reference;
                adjustment.CreatedBy = userId;
                adjustment.DateCreated = now;
            }

            _context.BillingAdjustments.AddRange(billingAdjustments);
            await _context.SaveChangesAsync();

            billing.BillingAdjustment = await _context.BillingAdjustments
                .Where(item => item.BillingReferenceId == reference)
                .SumAsync(item => item.Amount);
            billing.RemainingAmount = Math.Max(TMCWD.Model.Billing.BillingCharges.MeterMaintenanceFee,
                billing.TotalBillAmount - billing.BillingAdjustment);
            billing.UpdatedBy = userId;
            billing.DateUpdated = DateTime.Now;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return billingAdjustments;
        }

    }

}
