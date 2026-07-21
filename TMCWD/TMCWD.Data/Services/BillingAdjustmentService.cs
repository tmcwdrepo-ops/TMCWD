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
            if(billingAdjustment.Id == 0)
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
            return billingAdjustment;

        }

    }

}
