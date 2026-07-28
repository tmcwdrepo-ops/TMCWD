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

        public async Task<List<Reading>> GetByZoneAndBook(int zone, int book)
        {
            var res = from zoneBooks in _context.ZoneBooks
                      join readingSheets in _context.ReadingSheets on zoneBooks.Id equals readingSheets.ZoneBookId
                      join readings in _context.Readings on readingSheets.Id equals readings.ReadingSheetId
                      where zoneBooks.Zone == zone && zoneBooks.Book == book
                      select readings;
            return await res.ToListAsync();
        }

        public async Task<List<Reading>> GetByAccount(int accountId)
        {
            var readings = await _context.Readings.Where(x => x.AccountId == accountId).ToListAsync();
            return readings;
        }

        public async Task<Reading> GetCurrentByAccountZoneBook(int zone, int book, int accountId)
        {
            var data = await (from readings in _context.Readings
                              join readingSheets in _context.ReadingSheets on readings.ReadingSheetId equals readingSheets.Id
                              join zoneBooks in _context.ZoneBooks on readingSheets.ZoneBookId equals zoneBooks.Id
                              where zoneBooks.Zone == zone && zoneBooks.Book == book && readings.AccountId == accountId
                              orderby readingSheets.BillingDate descending
                              select readings).FirstOrDefaultAsync();
            return data;
        }

        public async Task<List<Reading>> GetByReadingSheetId(int readingSheetId)
        {
            var readings = await _context.Readings.Where(x => x.ReadingSheetId == readingSheetId).ToListAsync();
            return readings;
        }

        public async Task<Reading> GetAccountPreviousReading(int accountId)
        {
            var data = await (from readingSheets in _context.ReadingSheets
                       join readings in _context.Readings on readingSheets.Id equals readings.ReadingSheetId
                       where readings.AccountId == accountId
                       orderby readingSheets.BillingDate descending
                       select readings).ToListAsync();
            if (data == null || !data.Any()) return null;
            var previousReading = data.Skip(1).Take(1).FirstOrDefault();
            return previousReading;
        }

        public async Task<Reading> GetAccountCurrentReading(int accountId)
        {
            var data = await (from readingSheets in _context.ReadingSheets
                              join readings in _context.Readings on readingSheets.Id equals readings.ReadingSheetId
                              where readings.AccountId == accountId
                              orderby readingSheets.BillingDate descending
                              select readings).FirstOrDefaultAsync();
            return data;
        }

        public async Task<List<Reading>> GetByReader(int readerId)
        {
            var data = from readingSheets in _context.ReadingSheets
                       join readings in _context.Readings on readingSheets.Id equals readings.ReadingSheetId
                       where readingSheets.AssignedTo == readerId
                       select readings;
            return await data.ToListAsync();
        }

        public async Task<Reading> SaveUpdate(int userId, Reading reading)
        {
            if(reading.Id == 0)
            {
                reading.CreatedBy = userId;
                reading.DateCreated = DateTime.Now;
                _context.Readings.Add(reading);
            }
            else
            {
                var toUpdate = await _context.Readings.Where(x=>x.Id == reading.Id).FirstOrDefaultAsync();
                if(toUpdate != null)
                {
                    toUpdate = reading;
                    toUpdate.UpdatedBy = userId;
                    toUpdate.DateUpdated = DateTime.Now;
                    _context.Readings.Update(toUpdate);
                }
            }

            await _context.SaveChangesAsync();
            return reading;
        }

        public async Task<List<Reading>> SaveMultiple(List<Reading> readings)
        {
            await _context.Readings.AddRangeAsync([.. readings]);
            await _context.SaveChangesAsync();
            return readings;
        }

        public async Task<List<Reading>> GetReadingsByBillingPeriod(int zone, int book, DateTime billingPeriod)
        {
            var data = from zoneBooks in _context.ZoneBooks
                       join readingSheets in _context.ReadingSheets on zoneBooks.Id equals readingSheets.ZoneBookId
                       join readings in _context.Readings on readingSheets.Id equals readings.ReadingSheetId
                       where zoneBooks.Zone == zone && zoneBooks.Book == book && readingSheets.BillingDate == billingPeriod
                       select readings;
            return await data.ToListAsync();
        }

        public async Task<List<Reading>> GetRangeByReadingSheetIds(int[] ids)
        {
            var readings = await _context.Readings.Where(x => ids.Contains(x.ReadingSheetId)).ToListAsync();
            return readings;
        }

        #endregion

    }
}
