using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IBillingService
    {

        public Task<Billing> Get(int id);

        public Task<List<Billing>> GetAll();

        public Task<List<Billing>> GetByAccountId(int accountId);

        public Task<List<Billing>> GetByJobOrderId(int jobOrderId);

        public Task<Billing> GetByBillingReference(string billingReferenceId);

        public Task<Billing> GetByPaymentTransactionId(string paymentTransactionId);

        public Task<Billing> GetByReadingId(int readingId);

        public Task<List<Billing>> GetByReadings(int[] readingIds);

        public Task<Billing> SaveUpdate(int userId, Billing billing);

    }
}
