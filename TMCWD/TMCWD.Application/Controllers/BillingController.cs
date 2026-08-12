using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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

        #endregion

        #region methods

        public BillingController(AuthenticatedUserService user,
            BillingTransaction billingTrans,
            PenaltyTransaction penaltyTrans,
            AccountTransaction accountTrans)
        {
            _user = user;
            _billingTrans = billingTrans;
            _penaltyTrans = penaltyTrans;
            _accountTrans = accountTrans;
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
        public async Task<IActionResult> GetBillByBillPeriod(DateTime billPeriod) { 
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

        public IActionResult Collections()
        {
            return View();
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

        #endregion
    }
}