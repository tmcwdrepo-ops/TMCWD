using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class InspectionTypeService : IInspectionTypeService
    {

        private readonly UserDbContext _dbContext;

        public InspectionTypeService(UserDbContext context)
        {
            _dbContext = context;
        }

        public async Task<InspectionType?> Get(int id)
        {
            var inspectionType = await _dbContext.InspectionTypes.Where(x => x.Id == id).FirstOrDefaultAsync();
            return inspectionType;
        }

        public async Task<IEnumerable<InspectionType>?> GetTypes()
        {
            var inspectionTypes = _dbContext.InspectionTypes;
            return await inspectionTypes.ToListAsync();
        }

        public async Task<InspectionType> SaveUpdate(int userId, InspectionType type)
        {
            if(type.Id > 0)
            {
                var stored = await _dbContext.InspectionTypes.SingleAsync(t => t.Id == type.Id);
                stored.Name = type.Name; stored.WithDetail = type.WithDetail;
                stored.IsRequiredAccount = type.IsRequiredAccount; stored.IsNew = type.IsNew;
                stored.IsActive = type.IsActive;
                type = stored;
            }
            else
            {
                type.CreatedBy = userId;
                type.DateCreated = DateTime.Now;
                _dbContext.InspectionTypes.Add(type);
            }
            type.UpdatedBy = userId;
            type.DateUpdated = DateTime.Now;

            await _dbContext.SaveChangesAsync();

            return type;
        }

        public async Task<InspectionType?> GetNewRequestType()
        {
            var inspectionType = await _dbContext.InspectionTypes.Where(x => x.IsNew == true).FirstOrDefaultAsync();
            return inspectionType;
        }

    }
}
