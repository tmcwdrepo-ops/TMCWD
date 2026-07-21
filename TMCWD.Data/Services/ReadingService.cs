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
            var reading = await _context.Readings.Where(x => x.AccountId == accountId && x.BillingPeriod == billingPeriod).FirstOrDefaultAsync();
            return reading;
        }

        public async Task<List<Reading>> GetByReader(int readerId)
        {
            var reading = await _context.Readings.Where(x=>x.ReaderId == readerId).ToListAsync();
            return reading;
        }

        public async Task<List<Reading>> GetByZoneAndBook(int zone, int book)
        {
            //var readings = await _context.Readings.Where(x=> x.Zone == zone && x.Book == book).ToListAsync();
            var res = from zoneBooks in _context.ZoneBooks
                      join readings in _context.Readings on zoneBooks.Id equals readings.ZoneBookId
                      where zoneBooks.Zone == zone && zoneBooks.Book == book
                      select readings;
            return await res.ToListAsync();
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

        public async Task<Reading> GetAccountCurrentReading(int accountId)
        {
            var reading = await _context.Readings.Where(x => x.AccountId == accountId).Take(1).FirstOrDefaultAsync();
            return reading;
        }

        public async Task<Reading> GetAccountPreviousReading(int accountId)
        {
            var reading = await _context.Readings.Where(x => x.AccountId == accountId).OrderByDescending(x => x.BillingPeriod).Skip(1).Take(1).FirstOrDefaultAsync();
            return reading;
        }

        #endregion

    }
}
