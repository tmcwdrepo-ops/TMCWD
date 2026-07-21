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
            var otherCharges = await _context.OtherCharges.Where(x => x.BillingReferenceId == referenceId).ToListAsync();
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
    }
}
