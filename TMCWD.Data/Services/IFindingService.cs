using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IFindingService
    {

        Task<Finding?> Get(int id);
        Task<IEnumerable<Finding>> GetAll(int requestId);
        Task<Finding> SaveUpdate(int userId, int requestId, Finding finding);

    }
}
