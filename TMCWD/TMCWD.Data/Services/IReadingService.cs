using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IReadingService
    {
        public Task<Reading> Get(int id);

        public Task<List<Reading>> GetByZoneAndBook(int zone, int book);

        public Task<List<Reading>> GetByAccount(int accountId);

        public Task<Reading> GetCurrentByAccountZoneBook(int zone, int book, int accountId);

        public Task<List<Reading>> GetByReadingSheetId(int readingSheetId);

        public Task<Reading> GetAccountPreviousReading(int accountId);

        public Task<Reading> GetAccountCurrentReading(int accountId);

        public Task<List<Reading>> GetByReader(int readerId);

        public Task<Reading> SaveUpdate(int userId, Reading reading);

        public Task<List<Reading>> SaveMultiple(List<Reading> readings);

        public Task<List<Reading>> GetReadingsByBillingPeriod(int zone, int book, DateTime billingPeriod);

        public Task<List<Reading>> GetRangeByReadingSheetIds(IEnumerable<int> ids);

    }
}
