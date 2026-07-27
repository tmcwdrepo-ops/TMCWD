using Microsoft.AspNetCore.Mvc;
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

        #region ctor

        public ReadingSheetService(UserDbContext context)
        {
            _context = context;
        }

        #endregion

        #region methods

        public async Task<ReadingSheet> Get(int id)
        {
            var readingSheet = await _context.ReadingSheets.Where(x => x.Id == id).FirstOrDefaultAsync();
            return readingSheet;
        }

        public async Task<List<ReadingSheet>> GetAll()
        {
            var readingSheets = _context.ReadingSheets;
            return await readingSheets.ToListAsync();
        }

        public async Task<List<ReadingSheet>> GetByAssignedTo(int assignedTo)
        {
            var readingSheets = _context.ReadingSheets.Where(x => x.AssignedTo == assignedTo);
            return await readingSheets.ToListAsync();
        }

        public async Task<List<ReadingSheet>> GetByZoneAndBook(int zone, int book)
        {
            var sheets = from zoneBooks in _context.ZoneBooks
                         join readingSheets in _context.ReadingSheets on zoneBooks.Id equals readingSheets.ZoneBookId
                         where zoneBooks.Zone == zone && zoneBooks.Book == book
                         select readingSheets;
            return await sheets.ToListAsync();
        }

        public async Task<List<ReadingSheet>> GetByZoneBookAndAssignedTo(int zone, int book, int assignedTo)
        {
            //var readingSheets = _context.ReadingSheets.Where(x => x.Zone == zone && x.Book == book && x.AssignedTo == assignedTo);
            var sheets = from zoneBooks in _context.ZoneBooks
                         join readingSheets in _context.ReadingSheets on zoneBooks.Id equals readingSheets.ZoneBookId
                         where zoneBooks.Zone == zone && zoneBooks.Book == book && readingSheets.AssignedTo == assignedTo
                         select readingSheets;
            return await sheets.ToListAsync();
        }

        public async Task<List<ReadingSheet>> GetByBillingDate(int zone, int book, DateTime billingDate)
        {
            //var readingSheet = await _context.ReadingSheets.Where(x => x.Zone == zone && x.Book == book && x.BillingDate == billingDate).FirstOrDefaultAsync();
            var sheets = await (from zoneBooks in _context.ZoneBooks
                                join readingSheets in _context.ReadingSheets on zoneBooks.Id equals readingSheets.ZoneBookId
                                where zoneBooks.Zone == zone && zoneBooks.Book == book && readingSheets.BillingDate == billingDate
                                select readingSheets).ToListAsync();
            return sheets;
        }

        public async Task<ReadingSheet> GetByBillingDateAndAssignedTo(int zone, int book, DateTime billingDate, int assignedTo)
        {
            var sheets = await (from zoneBooks in _context.ZoneBooks
                                join readingSheets in _context.ReadingSheets on zoneBooks.Id equals readingSheets.ZoneBookId
                                where zoneBooks.Zone == zone && zoneBooks.Book == book && DateOnly.FromDateTime(readingSheets.BillingDate) == DateOnly.FromDateTime(billingDate)
                                && readingSheets.AssignedTo == assignedTo
                                select readingSheets).FirstOrDefaultAsync();
            return sheets;
        }

        public async Task<ReadingSheet> SaveUpdate(int userId, ReadingSheet readingSheet)
        {
            if (readingSheet.Id > 0)
            {
                var forUpdate = await _context.ReadingSheets.Where(x => x.ZoneBookId == readingSheet.ZoneBookId && x.BillingDate == readingSheet.BillingDate).FirstOrDefaultAsync();
                if (forUpdate != null)
                {
                    forUpdate.Name = readingSheet.Name;
                    forUpdate.BillingDate = readingSheet.BillingDate;
                    forUpdate.AssignedTo = readingSheet.AssignedTo;
                    forUpdate.ZoneBookId = readingSheet.ZoneBookId;
                    forUpdate.UpdatedBy = userId;
                    forUpdate.DateUpdated = DateTime.Now;
                    readingSheet = forUpdate;
                    _context.ReadingSheets.Update(readingSheet);
                }
            }
            else
            {
                readingSheet.CreatedBy = userId;
                readingSheet.DateCreated = DateTime.Now;
                _context.ReadingSheets.Add(readingSheet);
            }

            await  _context.SaveChangesAsync();

            return readingSheet;
        }

        public async Task<List<ReadingSheet>> GetByZoneBookId(int zoneBookId)
        {
            var readingSheets = await _context.ReadingSheets.Where(x => x.ZoneBookId == zoneBookId).ToListAsync();
            return readingSheets;
        }

        public async Task<ReadingSheet> GetByBillingPeriodStart(int zone, int book, DateTime billingPeriodStart)
        {
            var data = from readingSheets in _context.ReadingSheets
                       join zoneBooks in _context.ZoneBooks on readingSheets.ZoneBookId equals zoneBooks.Id
                       where zoneBooks.Zone == zone && zoneBooks.Book == book && readingSheets.BillingPeriodStart == billingPeriodStart
                       select readingSheets;
            return await data.FirstOrDefaultAsync();
        }

        public async Task<ReadingSheet> GetCurrentByAssignedTo(int zone, int book, int assignedTo)
        {
            var data = await (from readingSheets in _context.ReadingSheets
                              join zoneBooks in _context.ZoneBooks on readingSheets.ZoneBookId equals zoneBooks.Id
                              where zoneBooks.Zone == zone && zoneBooks.Book == book && readingSheets.AssignedTo == assignedTo
                              select readingSheets).OrderByDescending(x => x.BillingDate).FirstOrDefaultAsync();
            return data;
        }

        #endregion

    }
}
