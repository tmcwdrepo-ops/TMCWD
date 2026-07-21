using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
namespace TMCWD.Data.Services
{
    public class RequestService : IRequestService
    {

        private UserDbContext _dbContext;
        
        public RequestService(UserDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Request?> Get(int id)
        {

            var request = await _dbContext.Requests.Where(x => x.Id == id).FirstOrDefaultAsync();

            return request;
        }

        public async Task<IEnumerable<Request>?> GetAll()
        {
            var requests = _dbContext.Requests;
            return await requests.ToListAsync();
        }

        public async Task<Request> SaveUpdate(int userId, Request request)
        {
            if (request.Id > 0)
            {
                request.UpdatedBy = userId;
                request.DateUpdated = DateTime.Now;
                _dbContext.Requests.Update(request);
            }
            else
            {
                request.ControlNumber = string.Empty;
                request.CreatedBy = userId;
                request.DateCreated = DateTime.Now;

                var lastRequest = _dbContext.Requests.Where(x => x.DateCreated.Year == DateTime.Now.Year && x.DateCreated.Month == DateTime.Now.Month).OrderByDescending(x => x.ControlNumber).FirstOrDefault();
                if (lastRequest != null)
                {
                    int.TryParse(lastRequest.ControlNumber.Split('-')[^1].Trim(), out int lastCount);
                    if (lastCount > 0)
                    {
                        lastCount = lastCount + 1;
                        request.ControlNumber = $"TKT{request.DateCreated.ToString("yyyy")}-{request.DateCreated.ToString("MM")}-{(lastCount).ToString().PadLeft(4, '0')}";
                    }
                }

                if (String.IsNullOrEmpty(request.ControlNumber.Trim()))
                    request.ControlNumber = $"TKT{request.DateCreated.ToString("yyyy")}-{request.DateCreated.ToString("MM")}-{1.ToString().PadLeft(4, '0')}";

                _dbContext.Requests.Add(request);
            }

            await _dbContext.SaveChangesAsync();
            return request;
        }

        public async Task<IEnumerable<Request>?> GetByUserId(int userId)
        {
            var requests = _dbContext.Requests.Where(x => x.CreatedBy == userId);
            return await requests.ToListAsync();
        }

        public async Task<IEnumerable<Request>?> GetByCustomerId(int customerId)
        {
            var requests = _dbContext.Requests.Where(x => x.CustomerId == customerId);
            return await requests.ToListAsync();
        }
    }
}
