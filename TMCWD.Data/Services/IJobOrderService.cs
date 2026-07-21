using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IJobOrderService
    {
        Task<JobOrder?> Get(int id);
        Task<List<JobOrder>> GetAll(int requestId);
        Task<List<JobOrder>> GetByRequestId(int requestId);
        Task<JobOrder?> GetByRequestDetailId(int requestDetailId);
        Task<JobOrder> SaveUpdate(int userId, JobOrder jobOrder);
    }
}
