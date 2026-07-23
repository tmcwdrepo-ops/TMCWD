using TMCWD.Data.Entities;

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

        public Task<ReadingSheet> GetByBillingDate(int zone, int book, DateTime billingDate);

        public Task<ReadingSheet> GetByBillingPeriodStart(int zone, int book, DateTime billingPeriodStart);

        public Task<ReadingSheet> GetCurrentByAssignedTo(int zone, int book, int assignedTo);

        public Task<ReadingSheet> SaveUpdate(int userId, ReadingSheet readingSheet);

    }
}
