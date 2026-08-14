using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class OtherFeeTypeService : IOtherFeeTypeService
    {
        private readonly UserDbContext _context;

        public OtherFeeTypeService(UserDbContext context)
        {
            _context = context;
        }

        public async Task<OtherFeeType> Get(int id)
        {
            return await _context.OtherFeeTypes
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync();
        }

        public async Task<List<OtherFeeType>> GetAll()
        {
            return await _context.OtherFeeTypes
                .ToListAsync();
        }

        public async Task<List<OtherFeeType>> GetByName(string name)
        {
            return await _context.OtherFeeTypes
                .Where(x => x.Name == name)
                .ToListAsync();
        }

        public async Task<OtherFeeType> SaveUpdate(
            int userId,
            OtherFeeType otherFeeType)
        {
            if (otherFeeType.Id > 0)
            {
                otherFeeType.UpdatedBy = userId;
                otherFeeType.DateUpdate = DateTime.Now;

                _context.OtherFeeTypes.Update(otherFeeType);
            }
            else
            {
                otherFeeType.CreatedBy = userId;
                otherFeeType.DateCreated = DateTime.Now;

                otherFeeType.UpdatedBy = userId;
                otherFeeType.DateUpdate = DateTime.Now;

                _context.OtherFeeTypes.Add(otherFeeType);
            }

            await _context.SaveChangesAsync();

            return otherFeeType;
        }

        public async Task<bool> Delete(int id)
        {
            if (id <= 0)
                return false;

            var otherFeeType = await _context.OtherFeeTypes
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync();

            if (otherFeeType == null)
                return false;

            _context.OtherFeeTypes.Remove(otherFeeType);

            await _context.SaveChangesAsync();

            return true;
        }
    }
}