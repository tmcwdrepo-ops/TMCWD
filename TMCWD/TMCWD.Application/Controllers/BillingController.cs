using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TMCWD.Administration;
using TMCWD.Application.Models;
using TMCWD.Billing;
using TMCWD.CustomerSupport;
using TMCWD.Model.Administrator;
using TMCWD.Model.Billing;
using TMCWD.Model.Billing.Requests;
using TMCWD.Model.CustomerSupport;
using TMCWD.Services;

namespace TMCWD.Application.Controllers
{
    public class BillingController : Controller
    {

        #region constructors

        private readonly AuthenticatedUserService _user;
        private readonly UserTransaction _userTrans;
        private readonly BillingTransaction _billingTrans;
        private readonly PenaltyTransaction _penaltyTrans;
        private readonly AccountTransaction _accountTrans;
        private readonly ReadingTransaction _readingTrans;
        private readonly ReadingSheetTransaction _readingSheetTrans;
        private readonly CustomerTransaction _customerTrans;
        private readonly OtherChargeTransaction _otherChargeTrans;
        private readonly OtherFeeTypeTransaction _otherFeeTypeTrans;
        private readonly WaterRateTransaction _waterRateTrans;
        #endregion

        #region methods

        public BillingController(AuthenticatedUserService user,
            BillingTransaction billingTrans,
            PenaltyTransaction penaltyTrans,
            AccountTransaction accountTrans,
            ReadingTransaction readingTrans,
            CustomerTransaction customerTrans,
            ReadingSheetTransaction readingSheetTrans,
            OtherChargeTransaction otherChargeTrans,
            OtherFeeTypeTransaction otherFeeTypeTrans,
            UserTransaction userTrans,
            WaterRateTransaction waterRateTrans)
        {
            _user = user;
            _billingTrans = billingTrans;
            _penaltyTrans = penaltyTrans;
            _accountTrans = accountTrans;
            _readingTrans = readingTrans;
            _readingSheetTrans = readingSheetTrans;
            _customerTrans = customerTrans;
            _otherChargeTrans = otherChargeTrans;
            _otherFeeTypeTrans = otherFeeTypeTrans;
            _userTrans = userTrans;
            _waterRateTrans = waterRateTrans;
        }

        public async Task<IActionResult> BillAdjustment()
        {
            var readers = await _userTrans.GetUsersByRole(UserRole.MeterReader) ?? new List<User>();

            var model = new BillAdjustmentViewModel
            {
                BamDate = DateTime.Today,
                MeterReaders = readers.Select(r => new SelectListItem
                {
                    Value = r.Id.ToString(),
                    Text = r.Name
                }).ToList(),
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

        [HttpGet]
        public async Task<IActionResult> History(string accountNumber)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
                return BadRequest("Account number is required.");

            var account = await _accountTrans.GetByAccountNumber(accountNumber);
            if (account == null)
                return NotFound("Account not found.");

            var allBillings = await _billingTrans.GetAll() ?? new List<Model.Billing.Interfaces.BillingBase>();
            var accountBillings = allBillings
                .Where(b => b.AccountId == account.Id)
                .OrderByDescending(b => b.BillingPeriod)
                .ToList();

            var readings = (await _readingTrans.GetByAccount(account.Id) ?? new List<Model.Billing.Reading>())
                .OrderBy(r => r.Id)
                .ToList();

            var result = accountBillings.Select(billing =>
            {
                // Prefer an explicit ReadingId link if one exists and is valid.
                var reading = billing.ReadingId > 0
                    ? readings.FirstOrDefault(r => r.Id == billing.ReadingId)
                    : null;

                // Fallback: closest reading recorded on/before the billing period.
                if (reading == null)
                {
                    reading = readings
                        .Where(r => r.DateCreated <= billing.BillingPeriod)
                        .OrderByDescending(r => r.DateCreated)
                        .FirstOrDefault();
                }

                var present = reading?.CurrentReading ?? 0;
                var previous = reading?.PreviousReading ?? 0;
                var usage = Math.Max(0, present - previous);

                return new
                {
                    referenceNo = billing.BillingReferenceId,
                    billingDate = billing.BillingPeriod,
                    previous,
                    present,
                    usage,
                    amount = billing.TotalBillAmount
                };
            }).ToList();

            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAsBilledValues(string accountNumber, DateTime billingDate)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
                return BadRequest("Account number is required.");

            var account = await _accountTrans.GetByAccountNumber(accountNumber);
            if (account == null)
                return NotFound("Account not found.");

            var allBillings = await _billingTrans.GetAll() ?? new List<Model.Billing.Interfaces.BillingBase>();
            var billing = allBillings
                .Where(b => b.AccountId == account.Id && b.BillingPeriod.Date == billingDate.Date)
                .OrderByDescending(b => b.DateCreated)
                .FirstOrDefault();

            if (billing == null)
                return NotFound("No billing record found for this account and date.");

            var penalties = await _penaltyTrans.GetByReference(billing.BillingReferenceId) ?? new List<Model.Billing.Penalty>();
            var activePenaltyTotal = penalties
                .Where(p => p.PaymentStatus != PaymentStatus.Waived)
                .Sum(p => p.Amount);

            return Ok(new
            {
                currentBill = billing.TotalBillAmount,
                penalty = activePenaltyTotal
            });
        }

        public IActionResult Index() => View();
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
            //        accountNumber = account?.AccountNumber ?? "â€”",
            //        usage = 0,          // not yet tracked â€” see note
            //        billAmount = billing.TotalBillAmount,
            //        discount = 0,       // not yet tracked â€” see note
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


        [HttpGet]
        public async Task<IActionResult> GetChargeTypes()
        {
            var types = await _otherFeeTypeTrans.GetAll() ?? new List<OtherFeeType>();
            return Ok(types.Select(t => new { value = t.Id, label = t.Name }));
        }

        [HttpGet]
        public async Task<IActionResult> GetOtherChargesByAccount(string accountNumber)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
                return BadRequest("Account number is required.");

            var account = await _accountTrans.GetByAccountNumber(accountNumber);
            if (account == null)
                return NotFound("Account not found.");

            var allBillings = await _billingTrans.GetAll() ?? new List<Model.Billing.Interfaces.BillingBase>();
            var accountBillings = allBillings.Where(b => b.AccountId == account.Id).ToList();

            var result = new List<object>();

            foreach (var billing in accountBillings)
            {
                var charges = await _otherChargeTrans.GetByReference(billing.BillingReferenceId) ?? new List<Model.Billing.OtherCharge>();
                var activeCharges = charges.Where(c => c.IsActive).ToList();

                foreach (var charge in activeCharges)
                {
                    var feeType = await _otherFeeTypeTrans.Get(charge.Type);
                    result.Add(new
                    {
                        id = charge.Id,
                        description = feeType?.Name ?? "—",
                        payableIn = charge.PayableIn,
                        particulars = charge.Particulars,
                        amount = charge.Amount
                    });
                }
            }

            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> SaveOtherCharge([FromBody] SaveOtherChargeRequest request)
        {
            if (request == null)
                return BadRequest("Invalid request.");

            if (string.IsNullOrWhiteSpace(request.AccountNumber))
                return BadRequest("Account number is required.");

            if (request.ChargeType <= 0)
                return BadRequest("Charge type is required.");

            if (request.Amount <= 0)
                return BadRequest("Amount must be greater than zero.");

            var account = await _accountTrans.GetByAccountNumber(request.AccountNumber);
            if (account == null)
                return NotFound("Account not found.");

            var allBillings = await _billingTrans.GetAll() ?? new List<Model.Billing.Interfaces.BillingBase>();
            var currentBilling = allBillings
                .Where(b => b.AccountId == account.Id)
                .OrderByDescending(b => b.DateCreated)
                .FirstOrDefault();

            if (currentBilling == null)
            {
                Console.WriteLine("========== OTHER CHARGE ERROR ==========");
                Console.WriteLine($"Account Number: {request.AccountNumber}");
                Console.WriteLine($"Account ID: {account.Id}");
                Console.WriteLine("No billing record found for this account.");
                Console.WriteLine("========================================");

                return BadRequest("No billing record found for this account.");
            }

            var otherCharge = new Model.Billing.OtherCharge
            {
                BillingReferenceId = currentBilling.BillingReferenceId,
                Type = request.ChargeType,
                Amount = request.Amount,
                PayableIn = request.PayableIn,
                Particulars = request.Particulars,
                PaymentStatus = PaymentStatus.Unpaid,
                IsActive = true,
                DateCreated = DateTime.Now,
                DateUpdated = DateTime.Now
            };

            Model.Billing.OtherCharge saved;
            try
            {
                saved = await _otherChargeTrans.SaveUpdate(_user.User.Id, otherCharge);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"EXCEPTION calling OtherChargeTransaction.SaveUpdate: {ex.Message} | INNER: {ex.InnerException?.Message}");
            }

            if (saved == null)
            {
                return BadRequest("OtherChargeTransaction.SaveUpdate returned null — the API call to TMCWD.Data likely failed. Check TMCWD.Data is running and reachable.");
            }

            var feeType = await _otherFeeTypeTrans.Get(request.ChargeType);

            return Ok(new
            {
                id = saved.Id,
                description = feeType?.Name ?? "—",
                payableIn = saved.PayableIn,
                particulars = saved.Particulars,
                amount = saved.Amount
            });
        }

       

        [HttpPost]
        public async Task<IActionResult> DeactivateOtherCharge(int id)
        {
            var result = await _otherChargeTrans.Deactivate(id, _user.User.Id);
            if (result == null)
                return NotFound();

            return Ok(true);
        }

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

            // Group readings by AccountId â€” keep only the most recent per account
            var latestReading = readings
                .GroupBy(r => r.AccountId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Id).First());

            var result = new List<PresentReadingViewModel>();

            foreach (var account in accounts)
            {
                latestReading.TryGetValue(account.Id, out var reading);

                var present = reading?.CurrentReading ?? 0;
                var previous = reading?.PreviousReading ?? 0;

                decimal amount = 0;

                if (reading != null)
                {
                    amount = await WaterChargeCalculator.ComputeWaterCharge(
                        _waterRateTrans,
                        (int)previous,
                        (int)present,
                        account.Classification,
                        account.MeterSize > 0 ? account.MeterSize : 0.5m);
                }

                result.Add(new PresentReadingViewModel
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
                });
            }

            return Ok(result);
        }



        /// <summary>
        /// Live search â€” returns matching accounts (by account number, meter number, or address)
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

            // Fetch all readings in parallel â€” one Task per account instead of
            // two sequential awaits per account inside a foreach loop.
            var readingTasks = accounts.Select(a => _readingTrans.GetByAccount(a.Id));
            var allReadings = await Task.WhenAll(readingTasks);

            var result = new List<object>();

            for (int i = 0; i < accounts.Count; i++)
            {
                var account = accounts[i];
                var readings = allReadings[i];

                var current = readings?
                    .OrderByDescending(r => r.Id)
                    .FirstOrDefault();

                var present = current?.CurrentReading ?? 0;
                var previousVal = current?.PreviousReading ?? 0;

                decimal amount = 0;

                if (current != null)
                {
                    amount = await WaterChargeCalculator.ComputeWaterCharge(
                        _waterRateTrans,
                        (int)previousVal,
                        (int)present,
                        account.Classification,
                        account.MeterSize > 0 ? account.MeterSize : 0.5m);
                }

                var usage = Math.Max(0, present - previousVal);

                result.Add(new
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
                });
            }

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
                var usage = Math.Max(
                    0,
                    reading.CurrentReading - reading.PreviousReading);

                var amount = await WaterChargeCalculator.ComputeWaterCharge(
                    _waterRateTrans,
                    (int)reading.PreviousReading,
                    (int)reading.CurrentReading,
                    account.Classification,
                    account.MeterSize > 0 ? account.MeterSize : 0.5m);
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

        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> SaveReading([FromBody] SaveReadingRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest("Invalid request.");

                if (string.IsNullOrWhiteSpace(request.AccountNumber))
                    return BadRequest("Account number is required.");

                if (request.PresentReading < 0)
                    return BadRequest("Present reading must be a positive number.");

                if (request.Zone <= 0 || request.Book <= 0)
                    return BadRequest("Zone and book are required.");

                if (request.ReadingDate.Date >= request.BillingDate.Date)
                    return BadRequest(
                        "Billing date must be after the reading date.");

                // ============================================================
                // 1. GET ACCOUNT
                // ============================================================

                var account = await _accountTrans.GetByAccountNumber(request.AccountNumber);

                if (account == null)
                    return NotFound("Account not found.");

                // ============================================================
                // 2. GET EXISTING READING SHEET
                // ============================================================

                var existingSheets =
                    await _readingSheetTrans.GetByZoneAndBook(request.Zone, request.Book);

                var readingSheet = existingSheets?
                    .Where(s => s.Status == ReadingStatus.InProgress)
                    .OrderByDescending(s => s.BillingDate)
                    .FirstOrDefault();

                // ============================================================
                // 3. CREATE READING SHEET IF NONE EXISTS
                // ============================================================

                if (readingSheet == null)
                {
                    var billingDate = request.BillingDate.Date;

                    readingSheet = new TMCWD.Model.Billing.ReadingSheet
                    {
                        ZoneBookId = account.ZoneBookId,
                        BillingDate = billingDate,
                        DueDate = billingDate.AddDays(15),
                        Status = ReadingStatus.InProgress,
                        AssignedTo = request.ReaderId > 0
                            ? request.ReaderId
                            : 0
                    };

                    readingSheet =
                        await _readingSheetTrans.SaveUpdate(
                            _user.User.Id,
                            readingSheet);

                    if (readingSheet == null)
                        return StatusCode(
                            500,
                            "Failed to create reading sheet.");
                }

                // ============================================================
                // 4. GET THE LATEST READING
                // ============================================================
                //
                // DO NOT UPDATE IT.
                //
                // Its CurrentReading will become the PreviousReading
                // of the NEW record.
                //

                var accountReadings =
                    await _readingTrans.GetByAccount(account.Id);

                var latestReading = accountReadings?
                    .OrderByDescending(r => r.Id)
                    .FirstOrDefault();

                decimal previousReading =
                    latestReading?.CurrentReading ?? 0;

                // ============================================================
                // 5. DEBUG
                // ============================================================

                Console.WriteLine("========== SAVE READING ==========");
                Console.WriteLine($"Account ID       : {account.Id}");
                Console.WriteLine($"Account Number   : {account.AccountNumber}");
                Console.WriteLine($"Reading Sheet ID : {readingSheet.Id}");
                Console.WriteLine($"Latest Reading ID: {latestReading?.Id.ToString() ?? "NONE"}");
                Console.WriteLine($"Previous Reading : {previousReading}");
                Console.WriteLine($"New Reading      : {request.PresentReading}");
                Console.WriteLine("Action            : INSERT NEW READING");
                Console.WriteLine("==================================");

                // ============================================================
                // 6. CREATE A BRAND-NEW READING
                // ============================================================

                var reading = new TMCWD.Model.Billing.Reading
                {
                    AccountId = account.Id,

                    ReadingSheetId = readingSheet.Id,

                    // Previous reading comes from the latest record
                    PreviousReading = previousReading,

                    // New value entered by the meter reader
                    CurrentReading = request.PresentReading,

                    Status = ReadingStatus.InProgress,

                    IsCompleted = false
                };

                // ============================================================
                // 7. SAVE NEW READING
                // ============================================================

                var savedReading =
                    await _readingTrans.SaveUpdate(
                        _user.User.Id,
                        reading);

                if (savedReading == null)
                    return StatusCode(
                        500,
                        "Failed to save reading.");

                // ============================================================
                // 8. RETURN SUCCESS
                // ============================================================

                return Ok(savedReading);
            }
            catch (Exception ex)
            {
                Console.WriteLine("========== SAVE READING ERROR ==========");
                Console.WriteLine(ex);
                Console.WriteLine("========================================");

                return StatusCode(
                    500,
                    $"Error saving reading: {ex.Message}");
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
                    accountNumber = account?.AccountNumber ?? "â€”",
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
