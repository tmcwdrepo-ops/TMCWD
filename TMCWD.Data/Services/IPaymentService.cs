using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IPaymentService
    {

        public Task<Payment> Get(int id);

        public Task<Payment> GetByPaymentReference(string referenceId);

        public Task<List<Payment>> GetByBillingReference(string referenceId);

        public Task<List<Payment>> GetByMethod(int methodId);

        public Task<List<Payment>> GetByCashier(int userId);

        public Task<List<Payment>> GetByMacAddress(string macAddress);

        public Task<Payment> SaveUpdate(int userId, Payment payment);

    }
}
