using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdvancePaymentService : IAdvancePaymentService
    {

        #region fields

        private readonly UserDbContext _context;

        #endregion

        #region constructors

        public AdvancePaymentService(UserDbContext context)
        {
            _context = context;
        }

        #endregion

        #region methods

        [HttpGet("Get/{id}")]
        public async Task<AdvancePayment> Get(long id)
        {
            var advancePayment = await _context.AdvancePayments.Where(x => x.Id == id).FirstOrDefaultAsync();
            return advancePayment;
        }

        [HttpGet("GetActiveByAccount/{accountId}")]
        public async Task<AdvancePayment> GetActiveByAccount(int accountId)
        {
           var advancePayment = await _context.AdvancePayments.Where(x => x.AccountId == accountId && x.IsActive == true).FirstOrDefaultAsync();
            return advancePayment;
        }

        [HttpGet("GetAll")]
        public async Task<List<AdvancePayment>> GetAll()
        {
            var advancePayments = await _context.AdvancePayments.ToListAsync();
            return advancePayments;
        }

        [HttpGet("GetByAccount/{accountId}")]
        public async Task<List<AdvancePayment>> GetByAccount(int accountId)
        {
            var advancePayments = await _context.AdvancePayments.Where(x => x.AccountId == accountId).ToListAsync();
            return advancePayments;
        }

        [HttpGet("SaveUpdate/{userId}")]
        public async Task<AdvancePayment> SaveUpdate(int userId, AdvancePayment advancePayment)
        {
            if(advancePayment.Id == 0)
            {
                advancePayment.DateCreated = DateTime.Now;
                advancePayment.CreatedBy = userId;
                _context.AdvancePayments.Add(advancePayment);
            }
            else
            {
                var toUpdate = await Get(advancePayment.Id);
                advancePayment = toUpdate;
                advancePayment.UpdatedBy = userId;
                advancePayment.DateUpdated = DateTime.Now;
                _context.AdvancePayments.Update(advancePayment);
            }

            await _context.SaveChangesAsync();

            return advancePayment;
        }

        #endregion



    }
}
