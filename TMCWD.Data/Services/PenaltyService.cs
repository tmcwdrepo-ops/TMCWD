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
            if(penalty.Id == 0)
            {
                penalty.CreatedBy = userId;
                penalty.DateCreated = DateTime.Now;
                _context.Penalties.Add(penalty);
            }
            else
            {
                penalty.UpdatedBy = userId;
                penalty.DateUpdated = DateTime.Now;
                _context.Penalties.Update(penalty);
            }

            await _context.SaveChangesAsync();
            return penalty;
        }

    }
}
