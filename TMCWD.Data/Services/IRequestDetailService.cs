using System.Runtime.CompilerServices;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IRequestDetailService
    {

        Task<RequestDetail> SaveUpdate(int userId, int requestId, RequestDetail requestDetail);
        Task<IEnumerable<RequestDetail>> SaveMultiple(int userId, int requestId, List<RequestDetail> requestDetails);
        Task<IEnumerable<RequestDetail>?> GetByRequestId(int requestId);
        Task<RequestDetail?> Get(int id);
        Task<bool> Delete(RequestDetail detail);
        Task<bool> DeleteDetails(IEnumerable<RequestDetail> details);

    }
}
