using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class ApprovalHistoryService : IApprovalHistoryService
    {

        private readonly UserDbContext _context;

        public ApprovalHistoryService(UserDbContext context)
        {
            _context = context;
        }

        public async Task<List<ApprovalHistory>> GetAll(int jobOrderId)
        {
            var approvalHistories = await _context.ApprovalHistories.Where(x => x.JobOrderId == jobOrderId).ToListAsync();
            return approvalHistories;
        }

        public async Task<ApprovalHistory> Save(int userId, int jobOrderId, ApprovalHistory history)
        {
            history.CreatedBy = userId;
            history.JobOrderId = jobOrderId;
            history.DateCreated = DateTime.Now;
            _context.ApprovalHistories.Add(history);
            await _context.SaveChangesAsync();
            return history;
        }
    }
}
