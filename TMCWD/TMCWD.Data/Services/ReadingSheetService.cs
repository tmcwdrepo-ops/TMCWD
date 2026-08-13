using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class ReadingSheetService : IReadingSheetService
    {
        #region fields

        private readonly UserDbContext _context;

        #endregion

        #region constructor

        public ReadingSheetService(UserDbContext context)
        {
            _context = context;
        }

        #endregion

        #region methods

        public async Task<ReadingSheet> Get(int id)
        {
            var readingSheet = await _context.ReadingSheets
                .FirstOrDefaultAsync(x => x.Id == id);

            return readingSheet;
        }

        public async Task<List<ReadingSheet>> GetAll()
        {
            return await _context.ReadingSheets
                .ToListAsync();
        }

        public async Task<List<ReadingSheet>> GetByAssignedTo(int assignedTo)
        {
            return await _context.ReadingSheets
                .Where(x => x.AssignedTo == assignedTo)
                .ToListAsync();
        }

        public async Task<List<ReadingSheet>> GetByZoneAndBook(int zone, int book)
        {
            var sheets =
                from zoneBooks in _context.ZoneBooks
                join readingSheets in _context.ReadingSheets
                    on zoneBooks.Id equals readingSheets.ZoneBookId
                where zoneBooks.Zone == zone
                   && zoneBooks.Book == book
                select readingSheets;

            return await sheets.ToListAsync();
        }

        public async Task<List<ReadingSheet>> GetByZoneBookAndAssignedTo(
            int zone,
            int book,
            int assignedTo)
        {
            var sheets =
                from zoneBooks in _context.ZoneBooks
                join readingSheets in _context.ReadingSheets
                    on zoneBooks.Id equals readingSheets.ZoneBookId
                where zoneBooks.Zone == zone
                   && zoneBooks.Book == book
                   && readingSheets.AssignedTo == assignedTo
                select readingSheets;

            return await sheets.ToListAsync();
        }

        public async Task<List<ReadingSheet>> GetByBillingDate(
            int zone,
            int book,
            DateTime billingDate)
        {
            var sheets =
                from zoneBooks in _context.ZoneBooks
                join readingSheets in _context.ReadingSheets
                    on zoneBooks.Id equals readingSheets.ZoneBookId
                where zoneBooks.Zone == zone
                   && zoneBooks.Book == book
                   && readingSheets.BillingDate.HasValue
                   && readingSheets.BillingDate.Value.Date == billingDate.Date
                select readingSheets;

            return await sheets.ToListAsync();
        }

        public async Task<ReadingSheet?> GetByBillingDateAndAssignedTo(
            int zone,
            int book,
            DateTime billingDate,
            int assignedTo)
        {
            var sheet =
                await (
                    from zoneBooks in _context.ZoneBooks
                    join readingSheets in _context.ReadingSheets
                        on zoneBooks.Id equals readingSheets.ZoneBookId
                    where zoneBooks.Zone == zone
                       && zoneBooks.Book == book
                       && readingSheets.BillingDate.HasValue
                       && readingSheets.BillingDate.Value.Date == billingDate.Date
                       && readingSheets.AssignedTo == assignedTo
                    select readingSheets
                ).FirstOrDefaultAsync();

            return sheet;
        }

        public async Task<ReadingSheet> SaveUpdate(
            int userId,
            ReadingSheet readingSheet)
        {
            if (readingSheet.Id > 0)
            {
                var forUpdate =
                    await _context.ReadingSheets
                        .FirstOrDefaultAsync(x => x.Id == readingSheet.Id);

                if (forUpdate != null)
                {
                    forUpdate.Name = readingSheet.Name;
                    forUpdate.BillingDate = readingSheet.BillingDate;
                    forUpdate.DueDate = readingSheet.DueDate;
                    forUpdate.DisconnectionDate = readingSheet.DisconnectionDate;
                    forUpdate.BillingPeriodStart = readingSheet.BillingPeriodStart;
                    forUpdate.AssignedTo = readingSheet.AssignedTo;
                    forUpdate.ZoneBookId = readingSheet.ZoneBookId;
                    forUpdate.SeqFrom = readingSheet.SeqFrom;
                    forUpdate.SeqTo = readingSheet.SeqTo;
                    forUpdate.Status = readingSheet.Status;
                    forUpdate.DateUpload = DateTime.Now;

                    readingSheet = forUpdate;
                }
            }
            else
            {
                readingSheet.CreatedBy = userId;
                readingSheet.DateCreated = DateTime.Now;

                _context.ReadingSheets.Add(readingSheet);
            }

            await _context.SaveChangesAsync();

            return readingSheet;
        }

        public async Task<List<ReadingSheet>> GetByZoneBookId(int zoneBookId)
        {
            return await _context.ReadingSheets
                .Where(x => x.ZoneBookId == zoneBookId)
                .ToListAsync();
        }

        public async Task<ReadingSheet?> GetByBillingPeriodStart(
            int zone,
            int book,
            DateTime billingPeriodStart)
        {
            var sheet =
                await (
                    from zoneBooks in _context.ZoneBooks
                    join readingSheets in _context.ReadingSheets
                        on zoneBooks.Id equals readingSheets.ZoneBookId
                    where zoneBooks.Zone == zone
                       && zoneBooks.Book == book
                       && readingSheets.BillingPeriodStart.HasValue
                       && readingSheets.BillingPeriodStart.Value.Date ==
                          billingPeriodStart.Date
                    select readingSheets
                ).FirstOrDefaultAsync();

            return sheet;
        }

        public async Task<ReadingSheet?> GetCurrentByAssignedTo(
            int zone,
            int book,
            int assignedTo)
        {
            var sheet =
                await (
                    from zoneBooks in _context.ZoneBooks
                    join readingSheets in _context.ReadingSheets
                        on zoneBooks.Id equals readingSheets.ZoneBookId
                    where zoneBooks.Zone == zone
                       && zoneBooks.Book == book
                       && readingSheets.AssignedTo == assignedTo
                    orderby readingSheets.BillingDate descending
                    select readingSheets
                ).FirstOrDefaultAsync();

            return sheet;
        }

        public async Task<IEnumerable<ReadingSheet>> GetRangeReadingSheets(IEnumerable<long> ids)
        {
            var idsList = ids.ToList();

            return await _context.ReadingSheets
                .Where(x => idsList.Contains(x.Id))
                .ToListAsync();
        }

       public async Task<IEnumerable<ReadingSheet>> UpdateReadingSheetsStatus(
        IEnumerable<long> ids,
        int userId,
        int status)
        {
            var idsList = ids.ToList();

            var sheets = await _context.ReadingSheets
                .Where(x => idsList.Contains(x.Id))
                .ToListAsync();

            foreach (var sheet in sheets)
            {
                sheet.Status = (ReadingStatus)status;
            }

            await _context.SaveChangesAsync();

            return sheets;
        }

        public async Task<ReadingSheet?> GetCurrentByAssignedTo(int assignedTo)
        {
            return await _context.ReadingSheets
                .Where(x => x.AssignedTo == assignedTo)
                .OrderByDescending(x => x.BillingDate)
                .FirstOrDefaultAsync();
        }
        #endregion
    }
}