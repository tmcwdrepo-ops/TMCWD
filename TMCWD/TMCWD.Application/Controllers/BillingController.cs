using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TMCWD.Administration;
using TMCWD.Application.Models;
using TMCWD.Billing;
using TMCWD.CustomerSupport;
using TMCWD.Model.Administrator;
using TMCWD.Services;

namespace TMCWD.Application.Controllers
{
    public class BillingController : Controller
    {

        #region constructors

        private readonly AuthenticatedUserService _user;
        private readonly BillingTransaction _billingTrans;
        private readonly PenaltyTransaction _penaltyTrans;
        private readonly AccountTransaction _accountTrans;
        private readonly BillingAdjustmentTransaction _billingAdjustmentTrans;
        private readonly UserTransaction _userTrans;

        #endregion

        #region methods

        public BillingController(AuthenticatedUserService user,
             BillingTransaction billingTrans,
             PenaltyTransaction penaltyTrans,
             AccountTransaction accountTrans,
             BillingAdjustmentTransaction billingAdjustmentTrans,
             UserTransaction userTrans)
        {
            _user = user;
            _billingTrans = billingTrans;
            _penaltyTrans = penaltyTrans;
            _accountTrans = accountTrans;
            _billingAdjustmentTrans = billingAdjustmentTrans;
            _userTrans = userTrans;
        }

        public async Task<IActionResult> BillAdjustment()
        {
            var readers = await _userTrans.GetUsersByRole(UserRole.MeterReader) ?? new List<Model.Administrator.User>();

            var model = new BillAdjustmentViewModel
            {
                BamDate = DateTime.Today,
                MeterReaders = readers.Select(r => new SelectListItem { Value = r.Id.ToString(), Text = r.Name }).ToList(),
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitAdjustment(BillAdjustmentViewModel model)
        {
            async Task RestoreDropdowns()
            {
                model.BamDate ??= DateTime.Today;
                var readers = await _userTrans.GetUsersByRole(UserRole.MeterReader) ?? new List<Model.Administrator.User>();
                model.MeterReaders = readers.Select(r => new SelectListItem { Value = r.Id.ToString(), Text = r.Name }).ToList();
                model.RemarksOptions = new List<SelectListItem>
    {
        new SelectListItem { Value = "Meter Error",   Text = "Meter Error" },
        new SelectListItem { Value = "Reading Error", Text = "Reading Error" },
        new SelectListItem { Value = "System Error",  Text = "System Error" },
        new SelectListItem { Value = "Other",         Text = "Other" }
    };
            }

            // ── 1. Verify data is complete ──
            var checkedLines = model.AdjustmentLines.Where(l => l.IsChecked).ToList();

            if (string.IsNullOrWhiteSpace(model.AccountNumber) || !model.BillingDate.HasValue || !checkedLines.Any())
            {
                TempData["ErrorMessage"] = "Please provide an account number, billing date, and select at least one line item.";
                 await RestoreDropdowns();
                return View("BillAdjustment", model);
            }

            foreach (var line in checkedLines)
            {
                if (!line.AsBilled.HasValue || !line.ShouldBe.HasValue)
                {
                    TempData["ErrorMessage"] = $"'{line.Label}' is missing As Billed or Should Be values.";
                    await RestoreDropdowns();
                    return View("BillAdjustment", model);
                }
            }

            // Resolve account
            var account = await _accountTrans.GetByAccountNumber(model.AccountNumber);
            if (account == null)
            {
                TempData["ErrorMessage"] = $"No account found with number {model.AccountNumber}.";
                 await RestoreDropdowns();
                return View("BillAdjustment", model);
            }

            // Resolve the billing record for that account + billing date
            var accountBillings = await _billingTrans.GetByAccountId(account.Id) ?? new List<Model.Billing.Interfaces.BillingBase>();
            var billing = accountBillings.FirstOrDefault(b => b.BillingPeriod.Date == model.BillingDate.Value.Date);

            if (billing == null)
            {
                TempData["ErrorMessage"] = $"No billing record found for account {model.AccountNumber} on {model.BillingDate.Value:MM/dd/yyyy}.";
                RestoreDropdowns();
                return View("BillAdjustment", model);
            }

            // ── 2. Save data to TMCWD database ──
            decimal netAdjustment = 0;

            foreach (var line in checkedLines)
            {
                var adjustmentAmount = line.Adjustment ?? (line.ShouldBe.Value - line.AsBilled.Value);

                AdjustmentLineType lineType = line.Key switch
                {
                    "usage" => AdjustmentLineType.Usage,
                    "currentBill" => AdjustmentLineType.CurrentBill,
                    "penalty" => AdjustmentLineType.Penalty,
                    "present" => AdjustmentLineType.Present,
                    "previous" => AdjustmentLineType.Previous,
                    _ => AdjustmentLineType.CurrentBill
                };

                var adjustment = new Model.Billing.BillingAdjustment
                {
                    Type = (int)lineType,
                    BillingReferenceId = billing.BillingReferenceId,
                    Amount = adjustmentAmount
                };

                var saved = await _billingAdjustmentTrans.SaveUpdate(_user.User.Id, adjustment);

                if (saved == null)
                {
                    TempData["ErrorMessage"] = $"Failed to save adjustment for '{line.Label}'.";
                    RestoreDropdowns();
                    return View("BillAdjustment", model);
                }

                if (line.HasAdjustmentColumn) netAdjustment += adjustmentAmount;
            }

            // ── 3. Recompute billing ──
            var adjustments = await _billingAdjustmentTrans.GetByReference(billing.BillingReferenceId)
                  ?? new List<Model.Billing.BillingAdjustment>();

            var oldAdjustmentTotal = billing.BillingAdjustment;
            var newAdjustmentTotal = adjustments.Sum(x => x.Amount);
            var delta = newAdjustmentTotal - oldAdjustmentTotal;

            billing.TotalBillAmount += delta;
            billing.RemainingAmount += delta;
            billing.BillingAdjustment = newAdjustmentTotal;

            var savedBilling = await _billingTrans.SaveUpdate(_user.User.Id, billing);

            if (savedBilling == null)
            {
                TempData["ErrorMessage"] = "Adjustments were saved, but recomputing the billing total failed. Please review manually.";
                return RedirectToAction(nameof(BillAdjustment));
            }

            // ── 4. Notify — success ──
            TempData["SuccessMessage"] = $"Bill adjustment for {model.AccountNumber} submitted and recomputed successfully.";
            return RedirectToAction(nameof(BillAdjustment));
        }

        public IActionResult Index() => View();
        public IActionResult PenaltyCharging() => View();
        public IActionResult Penalty() => View();

        [HttpGet]

        public async  Task<IActionResult>GetBillByBillPeriod(DateTime billPeriod)
        {
            var allBillings = await _billingTrans.GetAll() ?? new List<Model.Billing.Interfaces.BillingBase>();
            var matching = allBillings.Where(b => b.BillingPeriod.Date == billPeriod.Date).ToList();

            var result = new List<object>();

            foreach (var billing in matching)
            {
                var penalties = await _penaltyTrans.GetByReference(billing.BillingReferenceId) ?? new List<Model.Billing.Penalty>();
                var activePenalties = penalties.Where(p => p.PaymentStatus != PaymentStatus.Waived).ToList();

                if (!activePenalties.Any()) continue; // only accounts with active penalty records

                if(billing.AccountId <= 0) continue;
                var account = await _accountTrans.Get((int)billing.AccountId);

                result.Add(new
                {
                    billingReferenceId = billing.BillingReferenceId,
                    accountNumber = account?.AccountNumber ?? "—",
                    usage = 0,          // not yet tracked — see note
                    billAmount = billing.TotalBillAmount,
                    discount = 0,       // not yet tracked — see note
                    penalty = activePenalties.Sum(p => p.Amount)
                });
            }

            return Ok(result);
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

        [HttpGet]
        public async Task<IActionResult> SearchAccounts(string query)
        {
            var billings = await _billingTrans.GetAll() ?? new List<Model.Billing.Interfaces.BillingBase>();
            var billedAccountIds = billings.Select(b => b.AccountId).Distinct().ToHashSet();

            var accounts = await _accountTrans.GetAccounts() ?? new List<Model.CustomerSupport.Account>();
            var matches = accounts
                .Where(a => billedAccountIds.Contains(a.Id))
                .Where(a => !string.IsNullOrEmpty(a.AccountNumber) &&
                            a.AccountNumber.Contains(query ?? "", StringComparison.OrdinalIgnoreCase))
                .Take(10)
                .Select(a => new { accountNumber = a.AccountNumber });

            return Ok(matches);
        }

        [HttpGet]
        public async Task<IActionResult> GetAsBilledValues(string accountNumber, DateTime billingDate)
        {
            var account = await _accountTrans.GetByAccountNumber(accountNumber);
            if (account == null) return NotFound();

            var billings = await _billingTrans.GetByAccountId(account.Id) ?? new List<Model.Billing.Interfaces.BillingBase>();
            var billing = billings.FirstOrDefault(b => b.BillingPeriod.Date == billingDate.Date);
            if (billing == null) return NotFound();

            var penalties = await _penaltyTrans.GetByReference(billing.BillingReferenceId) ?? new List<Model.Billing.Penalty>();
            var activePenaltyTotal = penalties.Where(p => p.PaymentStatus != PaymentStatus.Waived).Sum(p => p.Amount);

            return Ok(new { currentBill = billing.TotalBillAmount, penalty = activePenaltyTotal });
        }

        #endregion
    }
}