using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
using TMCWD.Model.Billing.Responses;

namespace TMCWD.Data.Services
{
    public class ReadingSheetService : IReadingSheetService
    {
        #region fields

        private readonly UserDbContext _context;
        private readonly IBillingService _billingService;
        private readonly IWaterRateService _waterRateService;

        #endregion

        #region constructor

        public ReadingSheetService(
            UserDbContext context,
            IBillingService billingService,
            IWaterRateService waterRateService)
        {
            _context = context;
            _billingService = billingService;
            _waterRateService = waterRateService;
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

        public async Task<bool> UpdateZoneProgress(
    int readingSheetId,
    int completedCount,
    int totalCount)
        {
            var readingSheet = await _context.ReadingSheets
                .FirstOrDefaultAsync(x => x.Id == readingSheetId);

            if (readingSheet == null)
                return false;

            // Prevent invalid progress values
            if (completedCount < 0)
                completedCount = 0;

            if (totalCount < 0)
                totalCount = 0;

            if (completedCount > totalCount)
                completedCount = totalCount;

            // If your ReadingSheet entity has progress fields,
            // update them here.

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<ReadingSheet?> GetCurrentByAssignedTo(int assignedTo)
        {
            return await _context.ReadingSheets
                .Where(x => x.AssignedTo == assignedTo)
                .OrderByDescending(x => x.BillingDate)
                .FirstOrDefaultAsync();
        }

        public async Task<List<ReadingSheetAccountDto>> GetAccountsForReadingSheet(int readingSheetId)
        {
            var currentReadings = await _context.Readings
                .Where(r => r.ReadingSheetId == readingSheetId)
                .Join(_context.Accounts,
                    r => r.AccountId,
                    a => a.Id,
                    (r, a) => new { Reading = r, Account = a })
                .Join(_context.Customers,
                    ra => ra.Account.CustomerId,
                    c => c.Id,
                    (ra, c) => new { ra.Reading, ra.Account, Customer = c })
                .ToListAsync();

            var result = new List<ReadingSheetAccountDto>();

            foreach (var item in currentReadings)
            {
                decimal prev = item.Reading.PreviousReading;
                decimal pres = item.Reading.CurrentReading;
                decimal usage = pres - prev;

                var fullName = string.Join(" ", new[]
                {
        item.Customer.Firstname,
        item.Customer.Middlename,
        item.Customer.Lastname
    }.Where(n => !string.IsNullOrWhiteSpace(n)));

                var address = string.Join(" ", new[]
                {
        item.Account.UnitNumber,
        item.Account.Building,
        item.Account.HouseNumber,
        item.Account.Street,
        item.Account.Barangay
    }.Where(a => !string.IsNullOrWhiteSpace(a)));

                result.Add(new ReadingSheetAccountDto
                {
                    Name = fullName,
                    Code = item.Account.AccountNumber,
                    Number = item.Account.AccountNumber,
                    Prev = prev,
                    Pres = pres,
                    Usage = usage,
                    Trend = usage > 0 ? "up" : usage < 0 ? "down" : "normal",
                    Balance = 0,
                    Amount = 0,
                    Total = 0,
                    Status = item.Reading.Status.ToString(),
                    Category = "normal",
                    Classification = (int)item.Account.Classification,
                    MeterSize = item.Account.MeterSize > 0 ? item.Account.MeterSize : 0.5m,
                    Address = address,
                    MeterNo = item.Account.MeterNumber
                });
            }
            return result;
        }

        /// <summary>
        /// Posts every account on this reading sheet that still has a
        /// pending reading (Created/InProgress) and a real reading entered.
        /// For each: computes the bill via the tiered water rate, creates
        /// a Billing record, and marks the Reading as Completed.
        /// Returns the number of accounts posted.
        /// </summary>
        public async Task<int> PartialPost(int readingSheetId, int userId)
        {
            var pendingReadings = await _context.Readings
                .Where(r => r.ReadingSheetId == readingSheetId
                         && (r.Status == ReadingStatus.Created
                             || r.Status == ReadingStatus.InProgress)
                         && r.CurrentReading > 0)
                .ToListAsync();

            if (!pendingReadings.Any())
                return 0;

            var accountIds = pendingReadings.Select(r => r.AccountId).Distinct().ToList();
            var accounts = await _context.Accounts
                .Where(a => accountIds.Contains(a.Id))
                .ToListAsync();

            int postedCount = 0;

            foreach (var reading in pendingReadings)
            {
                var account = accounts.FirstOrDefault(a => a.Id == reading.AccountId);
                if (account == null) continue;

                var meterSize = account.MeterSize > 0 ? account.MeterSize : 0.5m;

                var rate = await _waterRateService.GetByClassificationAndMeterSize(
                    (int)account.Classification,
                    meterSize);

                if (rate == null)
                    continue; // no rate configured — skip rather than fail the whole batch

                var amount = ComputeWaterCharge(
                    (int)reading.PreviousReading,
                    (int)reading.CurrentReading,
                    rate);

                if (amount < 0) continue; // present < previous — invalid, skip

                var billing = new Billing
                {
                    AccountId = account.Id,
                    ReadingId = reading.Id,
                    BillingReferenceId = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    BillingPeriod = DateTime.Now,
                    TotalBillAmount = amount,
                    RemainingAmount = amount,
                    PaymentStatus = (int)PaymentStatus.Unpaid,
                    CreatedBy = userId,
                    DateCreated = DateTime.Now,
                    UpdatedBy = userId,
                    DateUpdated = DateTime.Now
                };

                await _billingService.SaveUpdate(userId, billing);

                reading.Status = ReadingStatus.Completed;
                reading.UpdatedBy = userId;
                reading.DateUpdated = DateTime.Now;

                postedCount++;
            }

            await _context.SaveChangesAsync();

            return postedCount;
        }

        /// <summary>
        /// Marks a reading sheet as Completed — only allowed once every
        /// reading on it has already been posted (no Created/InProgress
        /// readings remain). Returns null if the sheet doesn't exist or
        /// still has pending readings.
        /// </summary>
        public async Task<ReadingSheet> CompleteReadingSheet(int readingSheetId, int userId)
        {
            var sheet = await _context.ReadingSheets
                .FirstOrDefaultAsync(x => x.Id == readingSheetId);

            if (sheet == null)
                return null;

            var pendingCount = await _context.Readings
                .Where(r => r.ReadingSheetId == readingSheetId
                         && (r.Status == ReadingStatus.Created
                             || r.Status == ReadingStatus.InProgress))
                .CountAsync();

            if (pendingCount > 0)
                return null; // caller should treat null as "cannot complete yet"

            sheet.Status = ReadingStatus.Completed;
            sheet.DateUpload = DateTime.Now;

            await _context.SaveChangesAsync();

            return sheet;
        }

        /// <summary>
        /// Pure tiered water-rate calculation. Duplicated from
        /// TMCWD.Billing.WaterChargeCalculator by necessity — that class
        /// works against TMCWD.Model.Billing.WaterRate via an HTTP
        /// transaction, while this runs directly against the DB-backed
        /// TMCWD.Data.Entities.WaterRate. Keep both in sync if the rate
        /// formula ever changes.
        /// </summary>
        private static decimal ComputeWaterCharge(
            int previousReading,
            int presentReading,
            WaterRate rate)
        {
            if (presentReading < previousReading) return -1;

            int usage = presentReading - previousReading;
            const decimal WaterMeterMaintenanceFee = 20.00m;
            decimal total;

            if (usage <= 10)
                total = rate.MinimumCharge;
            else if (usage <= 20)
                total = rate.MinimumCharge + ((usage - 10) * rate.Rate11To20);
            else if (usage <= 30)
                total = rate.MinimumCharge + (10 * rate.Rate11To20) + ((usage - 20) * rate.Rate21To30);
            else if (usage <= 40)
                total = rate.MinimumCharge + (10 * rate.Rate11To20) + (10 * rate.Rate21To30) + ((usage - 30) * rate.Rate31To40);
            else
                total = rate.MinimumCharge + (10 * rate.Rate11To20) + (10 * rate.Rate21To30) + (10 * rate.Rate31To40) + ((usage - 40) * rate.Rate41Up);

            return total + WaterMeterMaintenanceFee;
        }

        #endregion
    }
}