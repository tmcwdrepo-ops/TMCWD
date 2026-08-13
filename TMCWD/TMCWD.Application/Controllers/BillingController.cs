using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TMCWD.Application.Models;
using TMCWD.Billing;
using TMCWD.CustomerSupport;
using TMCWD.Model.Administrator;
using TMCWD.Services;
using TMCWD.Model.CustomerSupport;
using TMCWD.Model.Billing;
using TMCWD.Model.Billing.Requests;
namespace TMCWD.Application.Controllers
{
    public class BillingController : Controller
    {

        #region constructors

        private readonly AuthenticatedUserService _user;
        private readonly BillingTransaction _billingTrans;
        private readonly PenaltyTransaction _penaltyTrans;
        private readonly AccountTransaction _accountTrans;
        private readonly ReadingTransaction _readingTrans;
        private readonly ReadingSheetTransaction _readingSheetTrans;

        #endregion

        #region methods

        public BillingController(AuthenticatedUserService user,
            BillingTransaction billingTrans,
            PenaltyTransaction penaltyTrans,
            AccountTransaction accountTrans,
            ReadingTransaction readingTrans,
            ReadingSheetTransaction readingSheetTrans)
        {
            _user = user;
            _billingTrans = billingTrans;
            _penaltyTrans = penaltyTrans;
            _accountTrans = accountTrans;
            _readingTrans = readingTrans;
            _readingSheetTrans = readingSheetTrans;
        }

        public IActionResult BillAdjustment()
        {
            var model = new BillAdjustmentViewModel
            {
                BamDate = DateTime.Today,
                MeterReaders = new List<SelectListItem>
                {
                    new SelectListItem { Value = "MR001", Text = "Juan Dela Cruz" },
                    new SelectListItem { Value = "MR002", Text = "Maria Santos" },
                    new SelectListItem { Value = "MR003", Text = "Pedro Reyes" }
                },
                RemarksOptions = new List<SelectListItem>
                {
                    new SelectListItem { Value = "Meter Error",   Text = "Meter Error" },
                    new SelectListItem { Value = "Reading Error", Text = "Reading Error" },
                    new SelectListItem { Value = "System Error",  Text = "System Error" },
                    new SelectListItem { Value = "Other",         Text = "Other" }
                },
                AdjustmentLines = new List<AdjustmentLineItem>
                {
                    new AdjustmentLineItem { Key = "usage",       Label = "Usage",        HasAdjustmentColumn = true,  IsChecked = false },
                    new AdjustmentLineItem { Key = "currentBill", Label = "Current Bill", HasAdjustmentColumn = true,  IsChecked = false },
                    new AdjustmentLineItem { Key = "penalty",     Label = "Penalty",      HasAdjustmentColumn = true,  IsChecked = false },
                    new AdjustmentLineItem { Key = "present",     Label = "Present",      HasAdjustmentColumn = false, IsChecked = false },
                    new AdjustmentLineItem { Key = "previous",    Label = "Previous",     HasAdjustmentColumn = false, IsChecked = false }
                }
            };
            return View(model);
        }

        public IActionResult Index() => View();
        public IActionResult Collections() => View("Collections");
        public IActionResult PenaltyCharging() => View();
        public IActionResult Penalty() => View();

        public IActionResult Reading() => View("Reading");

        [HttpGet]
        public async Task<IActionResult> GetBillByBillPeriod(DateTime billPeriod)
        {
            // TODO: save to database
            TempData["SuccessMessage"] = "Bill adjustment submitted successfully.";
            return RedirectToAction(nameof(BillAdjustment));
        }

        public IActionResult OtherCharges()
        {
            return View("OtherCharges");
        }
        public IActionResult ReadingSheet(DateTime billPeriod)
        {
            //var allBillings = await _billingTrans.GetAll() ?? new List<Model.Billing.Interfaces.BillingBase>();
            //var matching = allBillings.Where(b => b.BillingPeriod.Date == billPeriod.Date).ToList();

            //var result = new List<object>();

            //foreach (var billing in matching)
            //{
            //    var penalties = await _penaltyTrans.GetByReference(billing.BillingReferenceId) ?? new List<Model.Billing.Penalty>();
            //    var activePenalties = penalties.Where(p => p.PaymentStatus != PaymentStatus.Waived).ToList();

            //    if (!activePenalties.Any()) continue; // only accounts with active penalty records

            //    if(billing.AccountId <= 0) continue;
            //    var account = await _accountTrans.Get((int)billing.AccountId);

            //    result.Add(new
            //    {
            //        billingReferenceId = billing.BillingReferenceId,
            //        accountNumber = account?.AccountNumber ?? "—",
            //        usage = 0,          // not yet tracked — see note
            //        billAmount = billing.TotalBillAmount,
            //        discount = 0,       // not yet tracked — see note
            //        penalty = activePenalties.Sum(p => p.Amount)
            //    });
            //}

            //return Ok(result);
            return View("ReadingSheet");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitAdjustment(BillAdjustmentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Repopulate dropdowns
                model.MeterReaders = new List<SelectListItem>
                {
                    new SelectListItem { Value = "MR001", Text = "Juan Dela Cruz" },
                    new SelectListItem { Value = "MR002", Text = "Maria Santos" },
                    new SelectListItem { Value = "MR003", Text = "Pedro Reyes" }
                };
                model.RemarksOptions = new List<SelectListItem>
                {
                    new SelectListItem { Value = "Meter Error", Text = "Meter Error" },
                    new SelectListItem { Value = "Reading Error", Text = "Reading Error" },
                    new SelectListItem { Value = "System Error", Text = "System Error" },
                    new SelectListItem { Value = "Other", Text = "Other" }
                };
                return View("BillAdjustment", model);
            }

            // TODO: Process the adjustment data
            // Save to database, etc.

            TempData["SuccessMessage"] = "Bill adjustment submitted successfully.";
            return RedirectToAction(nameof(BillAdjustment));
        }


        public async Task<IActionResult> GetBillById(int id)
        {
            return Ok();
        }
        public class WaivePenaltiesRequest
        {
            public List<string> BillingReferenceIds { get; set; } = new();
        }

        [HttpPost]
        public async Task<IActionResult> WaivePenalties([FromBody] WaivePenaltiesRequest request)
        {
            foreach (var refId in request.BillingReferenceIds)
            {
                var penalties = await _penaltyTrans.GetByReference(refId) ?? new List<Model.Billing.Penalty>();
                foreach (var penalty in penalties.Where(p => p.PaymentStatus != PaymentStatus.Waived))
                {
                    penalty.PaymentStatus = PaymentStatus.Waived;
                    await _penaltyTrans.SaveUpdate(_user.User.Id, penalty);
                }
            }

            return Ok();
        }

        public IActionResult DebitCreditMemo()
        {
            return View();
        }

        /// <summary>
        /// Returns all accounts and their current/previous readings for a given zone and book.
        /// Used to populate the Present Reading table.
        /// Returns all accounts for a zone/book with their latest reading (0/0 if none yet).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetReadingsByZoneBook(int zone, int book)
        {
            if (zone <= 0 || book <= 0)
                return BadRequest("Zone and book are required.");

            // Load all accounts for the zone/book and their readings in parallel
            var accountsTask = _accountTrans.GetByZoneAndBook(zone, book);
            var readingsTask = _readingTrans.GetByZoneAndBook(zone, book);
            await Task.WhenAll(accountsTask, readingsTask);

            var accounts = accountsTask.Result;
            var readings = readingsTask.Result ?? new List<TMCWD.Model.Billing.Reading>();

            if (accounts == null || !accounts.Any())
                return Ok(new List<PresentReadingViewModel>());

            // Group readings by AccountId — keep only the most recent per account
            var latestReading = readings
                .GroupBy(r => r.AccountId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Id).First());

            var result = accounts.Select(account =>
            {
                latestReading.TryGetValue(account.Id, out var reading);

                var present = reading?.CurrentReading ?? 0;
                var previous = reading?.PreviousReading ?? 0;
                var usage = Math.Max(0, present - previous);
                // Charge is based on the present reading value (current meter reading)
                // WaterMeterMaintenanceFee only added when a reading has actually been recorded
                var amount = reading != null ? ComputeWaterCharge((int)previous, (int)present, account.Classification, account.MeterSize > 0 ? account.MeterSize : 0.5m) : 0;

                return new PresentReadingViewModel
                {
                    ReadingId = reading?.Id ?? 0,
                    AccountId = account.Id,
                    AccountNumber = account.AccountNumber,
                    Name = account.FullAddress,
                    Classification = account.Classification.ToString(),
                    MeterNumber = account.MeterNumber,
                    Address = account.FullAddress,
                    PresentReading = present,
                    PreviousReading = previous,
                    Amount = amount,
                    MeterSize = account.MeterSize
                };
            }).ToList();

            return Ok(result);
        }

        #region Water Charge Computation

        // ── Tier boundary constants (shared across all classifications) ──────────
        private const int TierCap0 = 10;
        private const int TierCap1 = 20;
        private const int TierCap2 = 30;
        private const int TierCap3 = 40;

        /// <summary>
        /// Holds the per-cu.m tier rates for a classification group.
        /// Minimum charge is NOT stored here — it is size-dependent and
        /// looked up separately from <see cref="MinChargeByClassAndSize"/>.
        /// </summary>
        private readonly struct WaterRateSet
        {
            public readonly decimal Rate1, Rate2, Rate3, Rate4;
            public WaterRateSet(decimal r1, decimal r2, decimal r3, decimal r4)
                => (Rate1, Rate2, Rate3, Rate4) = (r1, r2, r3, r4);
        }

        // ── Per-cu.m tier rates keyed by classification ───────────────────────────
        private static readonly Dictionary<AccountClassification, WaterRateSet> RateByClassification = new()
        {
            { AccountClassification.Residential, new(18.25m, 19.55m, 20.90m, 23.50m) },
            { AccountClassification.Government,  new(18.25m, 19.55m, 20.90m, 23.50m) },
            { AccountClassification.Commercial,  new(36.50m, 39.10m, 41.80m, 47.00m) },
            { AccountClassification.Industrial,  new(36.50m, 39.10m, 41.80m, 47.00m) },
            { AccountClassification.CommercialA, new(31.90m, 34.20m, 36.55m, 41.10m) },
            { AccountClassification.CommercialB, new(27.35m, 29.30m, 31.35m, 35.25m) },
            { AccountClassification.CommercialC, new(22.80m, 24.40m, 26.10m, 29.35m) },
            { AccountClassification.Wholesale,   new(54.75m, 58.65m, 62.70m, 70.50m) },
            { AccountClassification.Bulk,        new(54.75m, 58.65m, 62.70m, 70.50m) },
        };

        // ── Minimum charge keyed by (classification, meterSize) ──────────────────
        // meterSize decimal values: 0.5=½", 0.75=¾", 1.0=1", 1.5=1½", 2.0=2", 3.0=3", 4.0=4"
        private static readonly Dictionary<(AccountClassification, decimal), decimal> MinChargeByClassAndSize = new()
        {
            // Residential / Government — same minimum charges per the official rate schedule
            { (AccountClassification.Residential, 0.50m),   170.00m }, { (AccountClassification.Government, 0.50m),   170.00m },
            { (AccountClassification.Residential, 0.75m),   272.00m }, { (AccountClassification.Government, 0.75m),   272.00m },
            { (AccountClassification.Residential, 1.00m),   544.00m }, { (AccountClassification.Government, 1.00m),   544.00m },
            { (AccountClassification.Residential, 1.50m), 1_360.00m }, { (AccountClassification.Government, 1.50m), 1_360.00m },
            { (AccountClassification.Residential, 2.00m), 3_400.00m }, { (AccountClassification.Government, 2.00m), 3_400.00m },
            { (AccountClassification.Residential, 3.00m), 6_120.00m }, { (AccountClassification.Government, 3.00m), 6_120.00m },
            { (AccountClassification.Residential, 4.00m),12_240.00m }, { (AccountClassification.Government, 4.00m),12_240.00m },

            // Commercial / Industrial
            { (AccountClassification.Commercial,  0.50m),   340.00m }, { (AccountClassification.Industrial, 0.50m),   340.00m },
            { (AccountClassification.Commercial,  0.75m),   544.00m }, { (AccountClassification.Industrial, 0.75m),   544.00m },
            { (AccountClassification.Commercial,  1.00m), 1_088.00m }, { (AccountClassification.Industrial, 1.00m), 1_088.00m },
            { (AccountClassification.Commercial,  1.50m), 2_720.00m }, { (AccountClassification.Industrial, 1.50m), 2_720.00m },
            { (AccountClassification.Commercial,  2.00m), 6_800.00m }, { (AccountClassification.Industrial, 2.00m), 6_800.00m },
            { (AccountClassification.Commercial,  3.00m),12_240.00m }, { (AccountClassification.Industrial, 3.00m),12_240.00m },
            { (AccountClassification.Commercial,  4.00m),24_480.00m }, { (AccountClassification.Industrial, 4.00m),24_480.00m },

            // Commercial A
            { (AccountClassification.CommercialA, 0.50m),   297.50m },
            { (AccountClassification.CommercialA, 0.75m),   476.00m },
            { (AccountClassification.CommercialA, 1.00m),   952.00m },
            { (AccountClassification.CommercialA, 1.50m), 2_380.00m },
            { (AccountClassification.CommercialA, 2.00m), 5_950.00m },
            { (AccountClassification.CommercialA, 3.00m),10_710.00m },
            { (AccountClassification.CommercialA, 4.00m),21_420.00m },

            // Commercial B
            { (AccountClassification.CommercialB, 0.50m),   255.00m },
            { (AccountClassification.CommercialB, 0.75m),   408.00m },
            { (AccountClassification.CommercialB, 1.00m),   816.00m },
            { (AccountClassification.CommercialB, 1.50m), 2_040.00m },
            { (AccountClassification.CommercialB, 2.00m), 5_100.00m },
            { (AccountClassification.CommercialB, 3.00m), 9_180.00m },
            { (AccountClassification.CommercialB, 4.00m),18_360.00m },

            // Commercial C
            { (AccountClassification.CommercialC, 0.50m),   212.50m },
            { (AccountClassification.CommercialC, 0.75m),   340.00m },
            { (AccountClassification.CommercialC, 1.00m),   680.00m },
            { (AccountClassification.CommercialC, 1.50m), 1_700.00m },
            { (AccountClassification.CommercialC, 2.00m), 4_250.00m },
            { (AccountClassification.CommercialC, 3.00m), 7_650.00m },
            { (AccountClassification.CommercialC, 4.00m),15_300.00m },

            // Wholesale / Bulk
            { (AccountClassification.Wholesale,   0.50m),   510.00m }, { (AccountClassification.Bulk, 0.50m),   510.00m },
            { (AccountClassification.Wholesale,   0.75m),   816.00m }, { (AccountClassification.Bulk, 0.75m),   816.00m },
            { (AccountClassification.Wholesale,   1.00m), 1_632.00m }, { (AccountClassification.Bulk, 1.00m), 1_632.00m },
            { (AccountClassification.Wholesale,   1.50m), 4_080.00m }, { (AccountClassification.Bulk, 1.50m), 4_080.00m },
            { (AccountClassification.Wholesale,   2.00m),10_200.00m }, { (AccountClassification.Bulk, 2.00m),10_200.00m },
            { (AccountClassification.Wholesale,   3.00m),18_360.00m }, { (AccountClassification.Bulk, 3.00m),18_360.00m },
            { (AccountClassification.Wholesale,   4.00m),36_720.00m }, { (AccountClassification.Bulk, 4.00m),36_720.00m },
        };

        // Flat fee applied to every bill regardless of classification, size, or usage
        private const decimal WaterMeterMaintenanceFee = 20.00m;

        /// <summary>
        /// Overload for call sites that have a single reading value (present reading as usage basis)
        /// and meter size, without a separate previous reading.
        /// </summary>
        private static decimal ComputeWaterCharge(int presentReading, AccountClassification classification, decimal meterSize)
            => ComputeWaterCharge(0, presentReading, classification, meterSize);

        /// <summary>
        /// Computes the water bill from meter readings, classification, and meter size.
        /// Minimum charge is looked up from the (classification, meterSize) table.
        /// Per-cu.m tier rates are looked up from the classification table.
        /// </summary>
        /// <param name="previousReading">The previous meter reading.</param>
        /// <param name="presentReading">The current meter reading.</param>
        /// <param name="classification">Account classification that determines tier rates.</param>
        /// <param name="meterSize">Meter size in inches as decimal (0.5, 0.75, 1.0, 1.5, 2.0, 3.0, 4.0).</param>
        /// <returns>
        /// Total peso amount due, or -1 if presentReading &lt; previousReading (meter rollover).
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if (classification, meterSize) is not found in the rate table.
        /// </exception>
        private static decimal ComputeWaterCharge(
            int previousReading,
            int presentReading,
            AccountClassification classification,
            decimal meterSize)
        {
            if (presentReading < previousReading)
                return -1; // meter rollover — flag for manual review

            if (!MinChargeByClassAndSize.TryGetValue((classification, meterSize), out var minCharge))
                throw new InvalidOperationException(
                    $"No minimum charge defined for classification '{classification}' with meter size {meterSize}\". " +
                    $"Valid sizes are: 0.5, 0.75, 1.0, 1.5, 2.0, 3.0, 4.0.");

            int usage = presentReading - previousReading;

            // Fee applies whenever a reading is taken — even if usage is 0
            if (usage <= 0) return WaterMeterMaintenanceFee;

            var rates = RateByClassification.TryGetValue(classification, out var r)
                ? r
                : throw new InvalidOperationException($"No tier rates defined for classification '{classification}'.");

            // Tier 0: 1–10 cu.m → flat minimum charge only
            decimal total = minCharge;
            if (usage <= TierCap0) return total + WaterMeterMaintenanceFee;

            // Tier 1: 11–20 cu.m
            int t1 = Math.Min(usage, TierCap1) - TierCap0; total += t1 * rates.Rate1;
            if (usage <= TierCap1) return total + WaterMeterMaintenanceFee;

            // Tier 2: 21–30 cu.m
            int t2 = Math.Min(usage, TierCap2) - TierCap1; total += t2 * rates.Rate2;
            if (usage <= TierCap2) return total + WaterMeterMaintenanceFee;

            // Tier 3: 31–40 cu.m
            int t3 = Math.Min(usage, TierCap3) - TierCap2; total += t3 * rates.Rate3;
            if (usage <= TierCap3) return total + WaterMeterMaintenanceFee;

            // Tier 4: 41+ cu.m — no cap
            int t4 = usage - TierCap3; total += t4 * rates.Rate4;
            return total + WaterMeterMaintenanceFee;
        }

        #endregion

        /// <summary>
        /// Live search — returns matching accounts (by account number, meter number, or address)
        /// together with each account's most recent reading. Used to populate the table as the
        /// user types in the Account Number field.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SearchAccounts(string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 1)
                return Ok(new List<object>());

            var accounts = await _accountTrans.Search(q.Trim());
            if (accounts == null || !accounts.Any())
                return Ok(new List<object>());

            // Fetch all readings in parallel — one Task per account instead of
            // two sequential awaits per account inside a foreach loop.
            var readingTasks = accounts.Select(a => _readingTrans.GetByAccount(a.Id));
            var allReadings = await Task.WhenAll(readingTasks);

            var result = accounts.Select((account, i) =>
            {
                var readings = allReadings[i];
                var current = readings?.OrderByDescending(r => r.Id).FirstOrDefault();
                var previous = readings?.OrderByDescending(r => r.Id).Skip(1).FirstOrDefault();

                var present = current?.CurrentReading ?? 0;
                var previousVal = current?.PreviousReading ?? 0;
                var usage = Math.Max(0, present - previousVal);
                var amount = ComputeWaterCharge((int)previousVal, (int)present, account.Classification, account.MeterSize > 0 ? account.MeterSize : 0.5m);

                return new
                {
                    accountId = account.Id,
                    accountNumber = account.AccountNumber,
                    name = account.FullAddress,
                    meterNumber = account.MeterNumber,
                    address = account.FullAddress,
                    classification = account.Classification.ToString(),
                    meterSize = account.MeterSize,
                    presentReading = present,
                    previousReading = previousVal,
                    usage,
                    amount
                };
            }).ToList<object>();

            return Ok(result);
        }

        /// <summary>
        /// Returns all reading records for a given account number.
        /// Used to populate the Present Reading table when looking up by account.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetReadingsByAccount(string accountNumber)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
                return BadRequest("Account number is required.");

            var account = await _accountTrans.GetByAccountNumber(accountNumber);
            if (account == null)
                return NotFound("Account not found.");

            var readings = await _readingTrans.GetByAccount(account.Id);

            if (readings == null || !readings.Any())
                return Ok(new List<PresentReadingViewModel>());

            var result = new List<PresentReadingViewModel>();
            foreach (var reading in readings)
            {
                var usage = Math.Max(0, reading.CurrentReading - reading.PreviousReading);
                var amount = ComputeWaterCharge((int)reading.PreviousReading, (int)reading.CurrentReading, account.Classification, account.MeterSize > 0 ? account.MeterSize : 0.5m);

                result.Add(new PresentReadingViewModel
                {
                    ReadingId = reading.Id,
                    AccountId = account.Id,
                    AccountNumber = account.AccountNumber,
                    Name = account.FullAddress,
                    Classification = account.Classification.ToString(),
                    MeterNumber = account.MeterNumber,
                    Address = account.FullAddress,
                    PresentReading = reading.CurrentReading,
                    PreviousReading = reading.PreviousReading,
                    Amount = amount,
                    MeterSize = account.MeterSize
                });
            }

            return Ok(result);
        }

        /// <summary>
        /// Returns account info and previous reading for a given account number.
        /// Used to auto-fill the Present Reading form fields.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAccountLookup(string accountNumber)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
                return BadRequest("Account number is required.");

            var account = await _accountTrans.GetByAccountNumber(accountNumber);
            if (account == null)
                return NotFound("Account not found.");

            // Get the current reading for this account (most recent)
            var currentReading = await _readingTrans.GetAccountCurrentReading(account.Id);

            var result = new AccountLookupViewModel
            {
                AccountId = account.Id,
                AccountNumber = account.AccountNumber,
                Name = account.FullAddress,
                MeterNumber = account.MeterNumber,
                Address = account.FullAddress,
                Classification = account.Classification.ToString(),
                // Use the CurrentReading value from the most recent reading record as "previous reading"
                // because that's what the user will be updating
                PreviousReading = currentReading?.CurrentReading ?? 0,
                MeterSize = account.MeterSize
            };

            return Ok(result);
        }

        /// <summary>
        /// Saves a new present reading for an account.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SaveReading([FromBody] SaveReadingRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.AccountNumber))
                    return BadRequest("Account number is required.");

                if (request.PresentReading < 0)
                    return BadRequest("Present reading must be a positive number.");

                if (request.Zone <= 0 || request.Book <= 0)
                    return BadRequest("Zone and book are required.");

                // Get account by account number
                var account = await _accountTrans.GetByAccountNumber(request.AccountNumber);
                if (account == null)
                    return NotFound("Account not found.");

                // Find the most recent InProgress reading sheet for this zone and book.
                // Using GetByZoneAndBook so we reuse whatever sheet already exists rather
                // than creating a new one every time the billing date doesn't match exactly.
                var existingSheets = await _readingSheetTrans.GetByZoneAndBook(request.Zone, request.Book);
                var readingSheet = existingSheets?
                    .Where(s => s.Status == ReadingStatus.InProgress)
                    .OrderByDescending(s => s.BillingDate)
                    .FirstOrDefault();

                if (readingSheet == null)
                {
                    // No active sheet — create one
                    var billingDate = request.BillingDate.Date;
                    readingSheet = new TMCWD.Model.Billing.ReadingSheet
                    {
                        ZoneBookId = account.ZoneBookId,
                        BillingDate = billingDate,
                        DueDate = billingDate.AddDays(15),
                        Status = ReadingStatus.InProgress,
                        AssignedTo = request.ReaderId > 0 ? request.ReaderId : 0
                    };
                    readingSheet = await _readingSheetTrans.SaveUpdate(_user.User.Id, readingSheet);

                    if (readingSheet == null)
                        return StatusCode(500, "Failed to create reading sheet.");
                }

                // Check if a reading already exists for this account in this reading sheet.
                // Search directly by AccountId to avoid misses caused by sheet ID mismatches.
                var accountReadings = await _readingTrans.GetByAccount(account.Id);
                var existingReading = accountReadings?
                    .Where(r => r.ReadingSheetId == readingSheet.Id)
                    .OrderByDescending(r => r.Id)
                    .FirstOrDefault()
                    // Fallback: if no reading exists in the current sheet, take the most recent
                    // reading for this account across any sheet so we can update it instead of
                    // inserting yet another duplicate.
                    ?? accountReadings?.OrderByDescending(r => r.Id).FirstOrDefault();

                TMCWD.Model.Billing.Reading reading;

                if (existingReading != null)
                {
                    // Update existing reading — shift current reading to previous before
                    // writing the new present reading value.
                    var fullExisting = await _readingTrans.Get(existingReading.Id);
                    if (fullExisting != null)
                    {
                        fullExisting.ReadingSheetId = readingSheet.Id;
                        fullExisting.PreviousReading = fullExisting.CurrentReading; // old present → previous
                        fullExisting.CurrentReading = request.PresentReading;      // new value
                        fullExisting.Status = ReadingStatus.InProgress;
                        fullExisting.IsCompleted = false;
                        reading = fullExisting;
                    }
                    else
                    {
                        return NotFound("Existing reading not found.");
                    }
                }
                else
                {
                    // First reading for this account — previous reading starts at 0
                    reading = new TMCWD.Model.Billing.Reading
                    {
                        AccountId = account.Id,
                        ReadingSheetId = readingSheet.Id,
                        CurrentReading = request.PresentReading,
                        PreviousReading = 0,
                        Status = ReadingStatus.InProgress,
                        IsCompleted = false
                    };
                }

                // Save through ReadingTransaction
                var savedReading = await _readingTrans.SaveUpdate(_user.User.Id, reading);

                if (savedReading == null)
                    return StatusCode(500, "Failed to save reading.");

                return Ok(savedReading);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error saving reading: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetPenaltiesByBillPeriod(DateTime billPeriod)
        {
            var allBillings = await _billingTrans.GetAll() ?? new List<Model.Billing.Interfaces.BillingBase>();
            var matching = allBillings.Where(b => b.BillingPeriod.Date == billPeriod.Date).ToList();

            var result = new List<object>();

            foreach (var billing in matching)
            {
                var penalties = await _penaltyTrans.GetByReference(billing.BillingReferenceId) ?? new List<Model.Billing.Penalty>();
                var activePenalties = penalties.Where(p => p.PaymentStatus != PaymentStatus.Waived).ToList();

                if (!activePenalties.Any()) continue;
                if (billing.AccountId <= 0) continue;

                var account = await _accountTrans.Get((int)billing.AccountId);

                result.Add(new
                {
                    billingReferenceId = billing.BillingReferenceId,
                    accountNumber = account?.AccountNumber ?? "—",
                    usage = 0,
                    billAmount = billing.TotalBillAmount,
                    discount = 0,
                    penalty = activePenalties.Sum(p => p.Amount)
                });
            }

            return Ok(result);
        }

        #endregion
    }
}

  