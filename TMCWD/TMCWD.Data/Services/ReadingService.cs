using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class ReadingService : IReadingService
    {
        #region fields

        private readonly UserDbContext _context;

        #endregion

        #region constructors

        public ReadingService(UserDbContext context)
        {
            _context = context;
        }

        #endregion

        #region methods

        public async Task<Reading> Get(int id)
        {
            var reading = await _context.Readings.Where(x => x.Id == id).FirstOrDefaultAsync();
            return reading;
        }

        public async Task<List<Reading>> GetByAccount(int accountId)
        {
            var readings = await _context.Readings.Where(x => x.AccountId == accountId).ToListAsync();
            return readings;
        }

        public async Task<Reading> GetByAccountAndBillingPeriod(int accountId, DateTime billingPeriod)
        {
            var reading = await (
                from r in _context.Readings
                join rs in _context.ReadingSheets on r.ReadingSheetId equals rs.Id
                where r.AccountId == accountId && rs.BillingDate.Value.Date == billingPeriod.Date
                select r
            ).FirstOrDefaultAsync();
            return reading;
        }

        public async Task<List<Reading>> GetByReader(int readerId)
        {
            var readings = await (
                from r in _context.Readings
                join rs in _context.ReadingSheets on r.ReadingSheetId equals rs.Id
                where rs.AssignedTo == readerId
                select r
            ).ToListAsync();
            return readings;
        }

        public async Task<List<Reading>> GetByZoneAndBook(int zone, int book)
        {
            var res =
                from zoneBooks in _context.ZoneBooks
                join readingSheets in _context.ReadingSheets on zoneBooks.Id equals readingSheets.ZoneBookId
                join readings in _context.Readings on readingSheets.Id equals readings.ReadingSheetId
                where zoneBooks.Zone == zone && zoneBooks.Book == book
                select readings;
            return await res.ToListAsync();
        }

        public async Task<Reading> SaveUpdate(int userId, Reading reading)
        {
            if (reading.Id == 0)
            {
                reading.CreatedBy = userId;
                reading.DateCreated = DateTime.Now;
                _context.Readings.Add(reading);
            }
            else
            {
                var toUpdate = await _context.Readings.Where(x => x.Id == reading.Id).FirstOrDefaultAsync();
                if (toUpdate != null)
                {
                    toUpdate.AccountId = reading.AccountId;
                    toUpdate.ReadingSheetId = reading.ReadingSheetId;
                    toUpdate.CurrentReading = reading.CurrentReading;
                    toUpdate.Status = reading.Status;
                    toUpdate.UpdatedBy = userId;
                    toUpdate.DateUpdated = DateTime.Now;
                    reading = toUpdate;
                }
            }

            await _context.SaveChangesAsync();
            return reading;
        }

        public async Task<Reading> GetAccountCurrentReading(int accountId)
        {
            var reading = await (
                from r in _context.Readings
                join rs in _context.ReadingSheets on r.ReadingSheetId equals rs.Id
                where r.AccountId == accountId
                orderby rs.BillingDate descending
                select r
            ).FirstOrDefaultAsync();
            return reading;
        }

        public async Task<Reading> GetAccountPreviousReading(int accountId)
        {
            var reading = await (
                from r in _context.Readings
                join rs in _context.ReadingSheets on r.ReadingSheetId equals rs.Id
                where r.AccountId == accountId
                orderby rs.BillingDate descending
                select r
            ).Skip(1).Take(1).FirstOrDefaultAsync();
            return reading;
        }

        public async Task<List<Reading>> SaveRange(List<Reading> readings)
        {
            _context.Readings.AddRange(readings);
            await _context.SaveChangesAsync();
            return readings;
        }

        #endregion
    }
}