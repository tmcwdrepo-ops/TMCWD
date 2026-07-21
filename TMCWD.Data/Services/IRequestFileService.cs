using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IRequestFileService
    {
        Task<RequestFile?> Get(int id);
        Task<List<RequestFile>> GetAll(int jobOrderId);
        Task<RequestFile?> SaveUpdate(int userId, int jobOrderId, RequestFile file);
        Task<List<RequestFile>> SaveRange(RequestFile[] files);
    }
}
