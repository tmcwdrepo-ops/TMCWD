
using TMCWD.Data.Entities;
namespace TMCWD.Data.Services
{
    public interface IRequestService
    {
        Task<Request> SaveUpdate(int userId, Request request);
        Task<Request?> Get(int id);
        Task<IEnumerable<Request>?> GetAll();
        Task<IEnumerable<Request>?> GetByUserId(int userId);
        Task<IEnumerable<Request>?> GetByCustomerId(int customerId);

    }
}
