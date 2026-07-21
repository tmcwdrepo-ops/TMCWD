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
                type.UpdatedBy = userId;
                type.DateUpdated = DateTime.Now;
                _dbContext.InspectionTypes.Update(type);
            }
            else
            {
                type.CreatedBy = userId;
                type.DateCreated = DateTime.Now;
                _dbContext.InspectionTypes.Add(type);
            }

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
