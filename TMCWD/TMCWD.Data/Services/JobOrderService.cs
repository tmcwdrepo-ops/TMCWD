using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class JobOrderService : IJobOrderService
    {
        private readonly UserDbContext _context;

        public JobOrderService(UserDbContext context)
        {
            _context = context;
        }

        public async Task<JobOrder?> Get(int id)
        {
            var jobOrder = await _context.JobOrders.Where(x => x.Id == id).FirstOrDefaultAsync();
            return jobOrder;
        }

        public async Task<List<JobOrder>> GetAll(int requestId)
        {
            var jobOrders = _context.JobOrders.Where(x => x.RequestId == requestId);
            return await jobOrders.ToListAsync();
        }

        public async Task<JobOrder?> GetByRequestDetailId(int requestDetailId)
        {
            var jobOrder = await _context.JobOrders.Where(x => x.RequestDetailId == requestDetailId).FirstOrDefaultAsync();
            return jobOrder;
        }

        public async Task<List<JobOrder>> GetByRequestId(int requestId)
        {
            var jobOrders = _context.JobOrders.Where(x => x.RequestId == requestId);
            return await jobOrders.ToListAsync();
        }

        public async Task<JobOrder> SaveUpdate(int userId, JobOrder jobOrder)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            jobOrder.DateUpdated = DateTime.Now;
            if(jobOrder.Id > 0)
            {
                var existing = await _context.JobOrders.AsNoTracking().SingleAsync(x => x.Id == jobOrder.Id);
                // Older job order forms do not send service dates or referrals.
                jobOrder.JobOrderDate ??= existing.JobOrderDate;
                jobOrder.ReferredToUserId ??= existing.ReferredToUserId;
                jobOrder.DateReferred ??= existing.DateReferred;
                jobOrder.UpdatedBy = userId;
                _context.JobOrders.Update(jobOrder);
            }
            else
            {
                string shortGuid = Guid.NewGuid().ToString("N").Substring(0, 5);
                jobOrder.JobOrderNumber = $"JO{DateTime.Now.ToString("yyyy")}-{DateTime.Now.ToString("dd")}-{shortGuid}";
                jobOrder.CreatedBy = userId;
                jobOrder.DateCreated = DateTime.Now;
                _context.JobOrders.Add(jobOrder);
            }

            await _context.SaveChangesAsync();
            var request = await _context.Requests.FirstOrDefaultAsync(x => x.Id == jobOrder.RequestId && x.IsServiceRequest);
            if (request != null)
            {
                var statuses = await _context.JobOrders.Where(x => x.RequestId == request.Id).Select(x => x.Status).ToListAsync();
                request.Status = statuses.All(x => x == (int)JobOrderStatus.Completed) ? (int)RequestStatus.Completed
                    : statuses.All(x => x == (int)JobOrderStatus.Completed || x == (int)JobOrderStatus.Rejected)
                        ? (int)RequestStatus.Rejected : (int)RequestStatus.InProgress;
                request.UpdatedBy = userId;
                request.DateUpdated = DateTime.Now;
                await _context.SaveChangesAsync();
            }
            await transaction.CommitAsync();
            return jobOrder;
        }

    }
}
