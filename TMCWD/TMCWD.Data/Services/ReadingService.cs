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
                where r.AccountId == accountId && rs.BillingDate.HasValue && rs.BillingDate.Value.Date == billingPeriod.Date
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
            // Serialize entry and posting for a sheet, including requests from
            // different application instances. The lock lasts until commit.
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var sheets = await _context.ReadingSheets
                .FromSqlInterpolated($"SELECT * FROM reading_sheets WHERE Id = {reading.ReadingSheetId} FOR UPDATE")
                .ToListAsync();
            var sheet = sheets.SingleOrDefault();
            if (sheet == null || (sheet.Status != ReadingStatus.Created && sheet.Status != ReadingStatus.InProgress))
                throw new InvalidOperationException("The reading sheet is no longer active.");

            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == reading.AccountId);
            if (account == null || account.ZoneBookId != sheet.ZoneBookId)
                throw new InvalidOperationException("The account does not belong to this reading sheet.");

            if (reading.CurrentReading < 0 || reading.CurrentReading < reading.PreviousReading)
                throw new InvalidOperationException("Present reading cannot be less than the previous reading.");

            var duplicates = await _context.Readings
                .Where(r => r.AccountId == reading.AccountId && r.ReadingSheetId == reading.ReadingSheetId
                         && r.Id != reading.Id).ToListAsync();
            if (duplicates.Count > 0)
                throw new InvalidOperationException("A reading already exists for this account and reading sheet. Reload the sheet before continuing.");

            if (reading.Id == 0)
            {
                reading.CreatedBy = userId;
                reading.DateCreated = reading.DateCreated == default
                    ? DateTime.Now
                    : reading.DateCreated;
                _context.Readings.Add(reading);
            }
            else
            {
                var toUpdate = await _context.Readings.Where(x => x.Id == reading.Id).FirstOrDefaultAsync();
                if (toUpdate != null)
                {
                    if (toUpdate.AccountId != reading.AccountId || toUpdate.ReadingSheetId != reading.ReadingSheetId
                        || toUpdate.Status == ReadingStatus.Completed || toUpdate.IsCompleted
                        || await _context.Billings.AnyAsync(b => b.ReadingId == reading.Id))
                        throw new InvalidOperationException("A posted reading cannot be changed or moved to another account or sheet.");
                    if (reading.CurrentReading < toUpdate.PreviousReading)
                        throw new InvalidOperationException("Present reading cannot be less than the stored previous reading.");
                    toUpdate.AccountId = reading.AccountId;
                    toUpdate.ReadingSheetId = reading.ReadingSheetId;
                    toUpdate.CurrentReading = reading.CurrentReading;
                    toUpdate.Status = reading.Status;
                    toUpdate.UpdatedBy = userId;
                    toUpdate.DateUpdated = DateTime.Now;
                    reading = toUpdate;
                }
                else
                    throw new InvalidOperationException("Reading not found.");
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
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

        public async Task<List<ReadingWithBillingDate>> GetByAccountWithBillingDate(int accountId)
        {
            var result = await (
                from r in _context.Readings
                join rs in _context.ReadingSheets on r.ReadingSheetId equals rs.Id
                where r.AccountId == accountId && rs.BillingDate.HasValue
                select new ReadingWithBillingDate
                {
                    Id = r.Id,
                    AccountId = r.AccountId,
                    CurrentReading = r.CurrentReading,
                    BillingDate = rs.BillingDate.Value
                }
            ).ToListAsync();
            return result;
        }

        public async Task<Reading> GetCurrentByAccountZoneBook(int zone, int book, int accountId)
        {
            var reading = await (
                from r in _context.Readings
                join rs in _context.ReadingSheets on r.ReadingSheetId equals rs.Id
                join zb in _context.ZoneBooks on rs.ZoneBookId equals zb.Id
                where r.AccountId == accountId && zb.Zone == zone && zb.Book == book
                orderby rs.BillingDate descending
                select r
            ).FirstOrDefaultAsync();
            return reading;
        }

        public async Task<List<Reading>> GetByReadingSheetId(int readingSheetId)
        {
            var readings = await _context.Readings.Where(x => x.ReadingSheetId == readingSheetId).ToListAsync();
            return readings;
        }

        public async Task<List<Reading>> SaveMultiple(List<Reading> readings)
        {
            _context.Readings.AddRange(readings);
            await _context.SaveChangesAsync();
            return readings;
        }

        public async Task<List<Reading>> SaveRange(List<Reading> readings)
        {
            return await SaveMultiple(readings);
        }

        public async Task<List<Reading>> UpdateStatusByReadingSheetIds(IEnumerable<int> readingSheetIds, int status, int userId)
        {
            var idsList = readingSheetIds.ToList();
            var readings = await _context.Readings.Where(x => idsList.Contains((int)x.ReadingSheetId)).ToListAsync();
            foreach (var reading in readings)
            {
                reading.Status = (ReadingStatus)status;
                reading.UpdatedBy = userId;
                reading.DateUpdated = DateTime.Now;
            }
            await _context.SaveChangesAsync();
            return readings;
        }

        public async Task<List<Reading>> GetReadingsByBillingPeriod(int zone, int book, DateTime billingPeriod)
        {
            var readings = await (
                from r in _context.Readings
                join rs in _context.ReadingSheets on r.ReadingSheetId equals rs.Id
                join zb in _context.ZoneBooks on rs.ZoneBookId equals zb.Id
                where zb.Zone == zone && zb.Book == book && rs.BillingDate.HasValue && rs.BillingDate.Value.Date == billingPeriod.Date
                select r
            ).ToListAsync();
            return readings;
        }

        public async Task<List<Reading>> GetRangeByReadingSheetIds(IEnumerable<long> ids)
        {
            var readings = await _context.Readings.Where(x => ids.Contains(x.ReadingSheetId)).ToListAsync();
            return readings;
        }

        public async Task<List<Reading>> GetByReadingSheetIds(List<long> readingSheetIds)
        {
            var readings = await _context.Readings
                .Where(x => readingSheetIds.Contains(x.ReadingSheetId))
                .ToListAsync();
            return readings;
        }

        public async Task<List<Reading>> GetRangeByReadingSheetIds(List<int> readingSheetIds)
        {
            return await _context.Readings
                .Where(r => readingSheetIds.Contains((int)r.ReadingSheetId))
                .ToListAsync();
        }

        #endregion
    }
}
