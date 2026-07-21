using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IApprovalHistoryService
    {
        
        Task<ApprovalHistory> Save(int userId, int jobOrderId, ApprovalHistory history);
        Task<List<ApprovalHistory>> GetAll(int jobOrderId);

    }
}
