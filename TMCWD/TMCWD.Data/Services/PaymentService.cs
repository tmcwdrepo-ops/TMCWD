using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class PaymentService : IPaymentService
    {

        #region fields

        private readonly UserDbContext _context;

        #endregion

        #region constructors

        public PaymentService(UserDbContext context)
        {
            _context = context;
        }

        #endregion

        #region methods

        public async Task<Payment> Get(int id)
        {
            var payment = await _context.Payments.Where(x => x.Id == id).FirstOrDefaultAsync();
            return payment;
        }

        public async Task<List<Payment>> GetByBillingReference(string referenceId)
        {
            var payments = await _context.Payments.Where(x => x.BillingReference.ToLower() == referenceId.ToLower()).ToListAsync();
            return payments;
        }

        public async Task<List<Payment>> GetByCashier(int userId)
        {
            var payments = await _context.Payments.Where(x => x.CreatedBy == userId).ToListAsync();
            return payments;
        }

        public async Task<List<Payment>> GetByMethod(int methodId)
        {
            var payments = await _context.Payments.Where(x => x.Method ==  methodId).ToListAsync();
            return payments;
        }

        public async Task<List<Payment>> GetByMacAddress(string macAddress)
        {
            var payments = await _context.Payments.Where(x => x.MacAddress.ToLower() == macAddress.ToLower()).ToListAsync();
            return payments;
        }

        public async Task<Payment> GetByPaymentReference(string referenceId)
        {
            var payment = await _context.Payments.Where(x=>x.PaymentReference.ToLower() == referenceId.ToLower()).FirstOrDefaultAsync();
            return payment;
        }

        public async Task<Payment> SaveUpdate(int userId, Payment payment)
        {
            if (payment.Id == 0)
            {
                payment.CreatedBy = userId;
                payment.DateCreated = DateTime.Now;
                _context.Payments.Add(payment);
            }
            else
            {
                payment.UpdatedBy = userId;
                payment.DateUpdated = DateTime.Now;
                _context.Payments.Update(payment);
            }

            await _context.SaveChangesAsync();

            return payment;

        }

        #endregion

    }
}
