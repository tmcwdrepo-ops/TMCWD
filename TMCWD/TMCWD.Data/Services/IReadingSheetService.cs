using TMCWD.Data.Entities;
using TMCWD.Model.Billing.Responses;

namespace TMCWD.Data.Services
{
    public interface IReadingSheetService
    {

        public Task<ReadingSheet> Get(int id);

        public Task<List<ReadingSheet>> GetAll();

        public Task<List<ReadingSheet>> GetByAssignedTo(int assignedTo);

        public Task<List<ReadingSheet>> GetByZoneAndBook(int zone, int book);

        public Task<List<ReadingSheet>> GetByZoneBookAndAssignedTo(int zone, int book, int assignedTo);

        public Task<List<ReadingSheet>> GetByZoneBookId(int zoneBookId);

        public Task<List<ReadingSheet>> GetByBillingDate(int zone, int book, DateTime billingDate);

        public Task<ReadingSheet> GetByBillingDateAndAssignedTo(int zone, int book, DateTime billingDate, int assignedTo);

        public Task<ReadingSheet> GetByBillingPeriodStart(int zone, int book, DateTime billingPeriodStart);

        public Task<ReadingSheet> GetCurrentByAssignedTo(int zone, int book, int assignedTo);

        Task<IEnumerable<ReadingSheet>> GetRangeReadingSheets(IEnumerable<long> ids);

        public Task<ReadingSheet> SaveUpdate(int userId, ReadingSheet readingSheet);

        Task<IEnumerable<ReadingSheet>> UpdateReadingSheetsStatus(IEnumerable<long> ids,int userId,int status);

        Task<bool> UpdateZoneProgress(int readingSheetId, int completedCount, int totalCount);

        Task<List<ReadingSheetAccountDto>> GetAccountsForReadingSheet(int readingSheetId);

        Task<int> PartialPost(int readingSheetId, int userId);
        Task<ReadingSheet> CompleteReadingSheet(int readingSheetId, int userId);
    }
}
