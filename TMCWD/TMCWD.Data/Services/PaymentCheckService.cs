using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class PaymentCheckService : IPaymentCheckService
    {

        #region fields

        private readonly UserDbContext _context;

        #endregion

        #region constructors

        public PaymentCheckService(UserDbContext context)
        {
            _context = context;
        }

        #endregion

        #region methods

        public async Task<PaymentCheck> Get(int id)
        {
            var paymentCheck = await _context.PaymentChecks.Where(x => x.Id == id).FirstOrDefaultAsync();
            return paymentCheck;
        }

        public async Task<List<PaymentCheck>> GetAll()
        {
            var paymentChecks = await _context.PaymentChecks.ToListAsync();
            return paymentChecks;
        }

        public async Task<PaymentCheck> GetByReference(string reference)
        {
            var paymentCheck = await _context.PaymentChecks.Where(x => x.PaymentReference == reference).FirstOrDefaultAsync();
            return paymentCheck;
        }

        public async Task<PaymentCheck> SaveUpdate(int userId, PaymentCheck paymentCheck)
        {
            if(paymentCheck.Id == 0)
            {
                paymentCheck.CreatedBy = userId;
                paymentCheck.DateCreated = DateTime.Now;
                _context.PaymentChecks.Add(paymentCheck);
            }
            else
            {
                var toUpdate = await _context.PaymentChecks.Where(x => x.Id == paymentCheck.Id).FirstOrDefaultAsync();
                if(toUpdate != null)
                {
                    paymentCheck = toUpdate;
                    paymentCheck.UpdatedBy = userId;
                    paymentCheck.DateUpdated = DateTime.Now;
                    _context.PaymentChecks.Update(paymentCheck);
                }
            }

            await _context.SaveChangesAsync();
            return paymentCheck;
        }

        #endregion

    }
}
