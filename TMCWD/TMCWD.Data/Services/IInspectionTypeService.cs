using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IInspectionTypeService
    {

        Task<IEnumerable<InspectionType>?> GetTypes();
        Task<InspectionType?> Get(int id);
        Task<InspectionType> SaveUpdate(int userId, InspectionType type);
        Task<InspectionType?> GetNewRequestType();

    }
}
