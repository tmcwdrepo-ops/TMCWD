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
        private readonly IAccountService _accountService;
        private readonly IReadingService _readingService;

        #endregion

        #region constructor

        public ReadingSheetService(
            UserDbContext context,
            IBillingService billingService,
            IWaterRateService waterRateService,
            IAccountService accountService,
            IReadingService readingService)
        {
            _context = context;
            _billingService = billingService;
            _waterRateService = waterRateService;
            _accountService = accountService;
            _readingService = readingService;
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
            var sheet = await Get(readingSheetId);
            if (sheet == null || sheet.Status == ReadingStatus.Deleted) return new();
            var roster = await _accountService.GetReadingSheetRoster(sheet);
            var readings = (await _readingService.GetByReadingSheetId(readingSheetId))
                .Where(r => r.Status != ReadingStatus.Deleted)
                .GroupBy(r => r.AccountId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Id).First());
            var customerIds = roster.Select(a => a.CustomerId).Distinct().ToList();
            var customers = await _context.Customers.Where(c => customerIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id);
            var readingIds = readings.Values.Select(r => r.Id).ToList();
            var billings = await _context.Billings.Where(b => readingIds.Contains(b.ReadingId)).ToListAsync();
            var result = new List<ReadingSheetAccountDto>();
            foreach (var account in roster)
            {
                readings.TryGetValue(account.Id, out var reading);
                customers.TryGetValue(account.CustomerId, out var customer);
                var previous = reading == null
                    ? await _readingService.GetPreviousForSheet(account.Id, readingSheetId) : null;
                var prev = reading?.PreviousReading ?? previous?.CurrentReading ?? account.InitialReading;
                decimal? pres = reading?.CurrentReading;
                decimal? usage = pres - prev;
                var billing = reading == null ? null : billings.FirstOrDefault(b => b.ReadingId == reading.Id);
                var fullName = customer == null ? account.AccountNumber : string.Join(" ", new[]
                {
                    customer.Firstname, customer.Middlename, customer.Lastname
                }.Where(n => !string.IsNullOrWhiteSpace(n)));
                var address = string.Join(" ", new[]
                {
                    account.UnitNumber, account.Building, account.HouseNumber, account.Street, account.Barangay
                }.Where(a => !string.IsNullOrWhiteSpace(a)));
                result.Add(new ReadingSheetAccountDto
                {
                    Name = fullName, Code = account.AccountNumber, Number = account.AccountNumber,
                    Prev = prev, Pres = pres, Usage = usage,
                    Trend = usage > 0 ? "up" : usage < 0 ? "down" : "normal",
                    Balance = 0, Amount = 0, Total = 0,
                    Status = reading?.Status.ToString() ?? "Pending", Category = "normal",
                    Classification = account.Classification,
                    MeterSize = account.MeterSize > 0 ? account.MeterSize : 0.5m,
                    Address = address, MeterNo = account.MeterNumber,
                    ReferenceNumber = billing?.BillingReferenceId, ReadingDate = reading?.DateCreated
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
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var sheets = await _context.ReadingSheets
                .FromSqlInterpolated($"SELECT * FROM reading_sheets WHERE Id = {readingSheetId} FOR UPDATE")
                .ToListAsync();
            var readingSheet = sheets.SingleOrDefault();

            if (readingSheet?.BillingDate == null)
                throw new InvalidOperationException("The reading sheet has no billing date.");
            if (readingSheet.Status != ReadingStatus.Created && readingSheet.Status != ReadingStatus.InProgress)
                throw new InvalidOperationException("Only active reading sheets can be posted.");

            var sheetReadings = await _context.Readings
                .Where(r => r.ReadingSheetId == readingSheetId).ToListAsync();
            if (sheetReadings.GroupBy(r => r.AccountId).Any(g => g.Count() > 1))
                throw new InvalidOperationException("This sheet contains duplicate account readings. Resolve the duplicates before posting.");

            var pendingReadings = sheetReadings
                .Where(r => (r.Status == ReadingStatus.Created
                             || r.Status == ReadingStatus.InProgress)
                         && (r.CurrentReading > 0 || r.Status == ReadingStatus.InProgress))
                .ToList();

            if (!pendingReadings.Any())
                return 0;

            var accountIds = pendingReadings.Select(r => r.AccountId).Distinct().ToList();
            var accounts = await _context.Accounts
                .Where(a => accountIds.Contains(a.Id))
                .ToListAsync();

            int postedCount = 0;

            var readingIds = pendingReadings.Select(r => r.Id).ToList();
            var billedReadingIds = await _context.Billings
                .Where(b => readingIds.Contains(b.ReadingId))
                .Select(b => b.ReadingId).ToListAsync();

            foreach (var reading in pendingReadings)
            {
                // Recover a previously persisted bill without creating it again.
                if (billedReadingIds.Contains(reading.Id))
                {
                    reading.Status = ReadingStatus.Completed;
                    reading.IsCompleted = true;
                    reading.UpdatedBy = userId;
                    reading.DateUpdated = DateTime.Now;
                    continue;
                }
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

                var lines = InvoiceChargeLines((int)reading.PreviousReading, (int)reading.CurrentReading, rate);
                var waterAmount = lines.Sum(line => line.Amount);
                var maintenance = TMCWD.Model.Billing.BillingCharges.MeterMaintenanceFee;
                if (waterAmount + maintenance != amount)
                    throw new InvalidOperationException("Invoice charge lines do not match the posted bill.");

                // The snapshot captures the water-charge components at the exact
                // post time so they are immune to future rate changes.
                // OtherCharges and BillingAdjustment start at zero — those are
                // posted after billing and are not part of the water invoice.
                // TotalDue = waterAmount + maintenance (the authoritative posted amount).
                // Freeze the consumption period from authoritative reading dates, not sheet scheduling dates.
                // PowerCostAdjustment and ArrearsAmount are not yet authoritative.
                // Penalty must NOT be stored in the snapshot.
                var previousReading = await _readingService.GetPreviousBeforeReading(reading.AccountId, reading.DateCreated);
                var snapshot = new TMCWD.Model.Billing.OfficialInvoiceSnapshot(
                    SchemaVersion: 1,
                    PeriodFrom: previousReading?.DateCreated,
                    PeriodTo: reading.DateCreated,
                    BillAmount: waterAmount,
                    MeterMaintenance: maintenance,
                    PowerCostAdjustment: null,
                    ArrearsAmount: null,
                    OtherCharges: 0m,
                    BillingAdjustment: 0m,
                    TotalDue: amount,       // waterAmount + maintenance = TotalBillAmount at post time
                    ChargeLines: lines);
                var billing = new Billing
                {
                    AccountId = account.Id,
                    ReadingId = reading.Id,
                    BillingReferenceId = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    BillingPeriod = readingSheet.BillingDate.Value.Date,
                    TotalBillAmount = amount,
                    RemainingAmount = amount,
                    InvoiceSnapshotJson = System.Text.Json.JsonSerializer.Serialize(snapshot),
                    PaymentStatus = (int)PaymentStatus.Unpaid,
                    CreatedBy = userId,
                    DateCreated = DateTime.Now,
                    UpdatedBy = userId,
                    DateUpdated = DateTime.Now
                };

                // Assign a stable, unique, customer-facing service invoice number
                // inside this transaction so the counter lock is held atomically.
                // Idempotent: a retry will skip already-billed reading IDs above,
                // so this path is only reached once per reading.
                await _billingService.AssignServiceInvoiceNumber(billing);

                _context.Billings.Add(billing);

                reading.Status = ReadingStatus.Completed;
                reading.IsCompleted = true;
                reading.UpdatedBy = userId;
                reading.DateUpdated = DateTime.Now;

                postedCount++;
            }

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return postedCount;
        }

        /// <summary>
        /// Marks a reading sheet as Completed — only allowed once every
        /// reading on it has already been posted (no Created/InProgress
        /// readings remain). Returns null if the sheet doesn't exist or
        /// still has pending readings.
        /// </summary>
        public async Task<ReadingSheet?> CompleteReadingSheet(int readingSheetId, int userId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var locked = await _context.ReadingSheets
                .FromSqlInterpolated($"SELECT * FROM reading_sheets WHERE Id = {readingSheetId} FOR UPDATE").ToListAsync();
            var sheet = locked.SingleOrDefault();
            if (sheet == null || sheet.Status == ReadingStatus.Deleted) return null;
            if (sheet.Status == ReadingStatus.Completed) return sheet;
            if (sheet.Status != ReadingStatus.Created && sheet.Status != ReadingStatus.InProgress) return null;
            var roster = await _accountService.GetReadingSheetRoster(sheet);
            var readings = await _readingService.GetByReadingSheetId(readingSheetId);
            if (roster.Count == 0 || readings.GroupBy(r => r.AccountId).Any(g => g.Count() != 1)
                || readings.Any(r => r.Status != ReadingStatus.Completed || !r.IsCompleted)
                || roster.Any(a => !readings.Any(r => r.AccountId == a.Id))) return null;
            var bills = await _billingService.GetByReadings(readings.Select(r => checked((int)r.Id)).ToArray());
            if (readings.Any(r => !bills.Any(bill => bill.ReadingId == r.Id))) return null;
            sheet.Status = ReadingStatus.Completed;
            sheet.DateUpload = DateTime.Now;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
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

            return InvoiceChargeLines(previousReading, presentReading, rate).Sum(line => line.Amount)
                + TMCWD.Model.Billing.BillingCharges.MeterMaintenanceFee;
        }

        private static IReadOnlyList<TMCWD.Model.Billing.InvoiceChargeLine> InvoiceChargeLines(int previous, int current, WaterRate rate)
        {
            var usage = current - previous;
            var lines = new List<TMCWD.Model.Billing.InvoiceChargeLine>
            {
                new("Minimum (0-10 cu.m)", null, rate.MinimumCharge, rate.MinimumCharge, 0)
            };
            foreach (var (start, end, price, description) in new[] {
                (10, 20, rate.Rate11To20, "11-20 cu.m"),
                (20, 30, rate.Rate21To30, "21-30 cu.m"),
                (30, 40, rate.Rate31To40, "31-40 cu.m"),
                (40, int.MaxValue, rate.Rate41Up, "41+ cu.m") })
            {
                var quantity = Math.Max(0, Math.Min(usage, end) - start);
                if (quantity > 0) lines.Add(new(description, quantity, price, quantity * price, lines.Count));
            }
            return lines;
        }

        #endregion
    }
}
