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
            jobOrder.DateUpdated = DateTime.Now;
            if(jobOrder.Id > 0)
            {
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
            return jobOrder;
        }

    }
}
