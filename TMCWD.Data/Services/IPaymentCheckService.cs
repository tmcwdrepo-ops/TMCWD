using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IPaymentCheckService
    {
        public Task<PaymentCheck> Get(int id);

        public Task<List<PaymentCheck>> GetAll();

        public Task<PaymentCheck> GetByReference(string reference);

        public Task<PaymentCheck> SaveUpdate(int userId, PaymentCheck payment);
    }
}
