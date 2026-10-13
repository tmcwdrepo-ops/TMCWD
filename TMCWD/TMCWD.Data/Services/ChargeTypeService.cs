using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class ChargeTypeService : IChargeTypeService
    {

        private readonly UserDbContext _context;

        public ChargeTypeService(UserDbContext context)
        {
            _context = context;
        }
        public async Task<ChargeType> Get(int id)
        {
            var chargeType = await _context.ChargeTypes.Where(x => x.Id == id).FirstOrDefaultAsync();
            return chargeType;
        }
        public async Task<List<ChargeType>> GetAll()
        {
            var chargeTypes = await _context.ChargeTypes.ToListAsync();
            return chargeTypes;
        }
        public async Task<List<ChargeType>> GetByClassificationId(int classificationId)
        {
            var chargeTypes = await _context.ChargeTypes.Where(x => x.ClassificationId == classificationId).ToListAsync();
            return chargeTypes;
        }
        public async Task<ChargeType> SaveUpdate(int userId, ChargeType chargeType)
        {
            if (chargeType.Id == 0)
            {
                chargeType.CreatedBy = userId;
                chargeType.DateCreated = DateTime.Now;
                chargeType.UpdatedBy = userId;
                chargeType.DateUpdated = DateTime.Now;
                _context.ChargeTypes.Add(chargeType);

            }
            else
            {
                var existing = await Get(checked((int)chargeType.Id))
                    ?? throw new InvalidOperationException("Charge type was not found.");
                existing.Name = chargeType.Name;
                existing.ClassificationId = chargeType.ClassificationId;
                chargeType = existing;
                chargeType.UpdatedBy = userId;
                chargeType.DateUpdated = DateTime.Now;
                _context.ChargeTypes.Update(chargeType);
            }

            await _context.SaveChangesAsync();
            return chargeType;
        }
    }
}
