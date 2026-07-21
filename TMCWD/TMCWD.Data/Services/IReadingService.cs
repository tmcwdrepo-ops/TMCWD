using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IReadingService
    {
        public Task<Reading> Get(int id);

        public Task<List<Reading>> GetByZoneAndBook(int zone, int book);

        public Task<List<Reading>> GetByAccount(int accountId);

        public Task<Reading> GetByAccountAndBillingPeriod(int accountId, DateTime billingPeriod);

        public Task<Reading> GetAccountPreviousReading(int accountId);

        public Task<Reading> GetAccountCurrentReading(int accountId);

        public Task<List<Reading>> GetByReader(int readerId);

        public Task<Reading> SaveUpdate(int userId, Reading reading);

    }
}
