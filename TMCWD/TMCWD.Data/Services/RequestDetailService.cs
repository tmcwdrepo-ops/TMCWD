using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class RequestDetailService : IRequestDetailService
    {

        private readonly UserDbContext _dbContext;

        public RequestDetailService(UserDbContext context)
        {
            _dbContext = context;
        }

        public async Task<RequestDetail?> Get(int id)
        {
            var requestDetail = await _dbContext.RequestDetails.Where(x => x.Id == id).FirstOrDefaultAsync();
            return requestDetail;
        }

        public async Task<IEnumerable<RequestDetail>?> GetByRequestId(int requestId)
        {
            var requestDetails = _dbContext.RequestDetails.Where(x => x.RequestId == requestId);
            return await requestDetails.ToListAsync();
        }

        public async Task<IEnumerable<RequestDetail>> SaveMultiple(int userId, int requestId, List<RequestDetail> requestDetails)
        {
            bool isUpdate = false;
            foreach (var requestDetail in requestDetails)
            {
                requestDetail.RequestId = requestId;
                if (requestDetail.Id > 0)
                {
                    isUpdate = true;
                    requestDetail.DateUpdated = DateTime.Now;
                }
                else
                    requestDetail.DateCreated = DateTime.Now;
            }

            if (isUpdate) _dbContext.RequestDetails.UpdateRange(requestDetails);
            else await _dbContext.RequestDetails.AddRangeAsync(requestDetails);

            var res = await _dbContext.SaveChangesAsync();
            return requestDetails;
        }

        public async Task<RequestDetail> SaveUpdate(int userId, int requestId, RequestDetail requestDetail)
        {
            requestDetail.RequestId = requestId;

            if (requestDetail.Id > 0)
            {
                requestDetail.DateUpdated = DateTime.Now;
                _dbContext.RequestDetails.Update(requestDetail);
            }
            else
            {
                requestDetail.DateCreated = DateTime.Now;
                _dbContext.RequestDetails.Add(requestDetail);
            }

            await _dbContext.SaveChangesAsync();
            return requestDetail;
        }

        public async Task<bool> Delete(RequestDetail detail)
        {
            _dbContext.RequestDetails.Remove(detail);
            var res = await _dbContext.SaveChangesAsync();
            return res > 0;
        }

        public async Task<bool> DeleteDetails(IEnumerable<RequestDetail> details)
        {
            _dbContext.RequestDetails.RemoveRange(details);
            var res = await _dbContext.SaveChangesAsync();
            return res > 0;
        }
    }
}
