using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TMCWD.Application.Models;
using TMCWD.Billing;
using TMCWD.Services;

namespace TMCWD.Application.Controllers
{
    public class BillingController : Controller
    {

        #region fields

        //private readonly AuthenticatedUserService _user;
        //private readonly BillingTransaction _billTransaction;

        #endregion

        #region constructors

        //public BillingController(AuthenticatedUserService user, BillingTransaction billTransaction)
        //{
        //    _user = user;
        //    _billTransaction = billTransaction;
        //}

        public BillingController()
        {
            //_user = user;
            //_billTransaction = billTransaction;
        }

        #endregion

        #region methods

        public IActionResult ReadingSheet()
        {
            return View();
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
                    new SelectListItem { Value = "Meter Error", Text = "Meter Error" },
                    new SelectListItem { Value = "Reading Error", Text = "Reading Error" },
                    new SelectListItem { Value = "System Error", Text = "System Error" },
                    new SelectListItem { Value = "Other", Text = "Other" }
                },
                AdjustmentLines = new List<AdjustmentLineItem>
                {
                    new AdjustmentLineItem { Key = "usage", Label = "Usage", HasAdjustmentColumn = true, IsChecked = false },
                    new AdjustmentLineItem { Key = "currentBill", Label = "Current Bill", HasAdjustmentColumn = true, IsChecked = false },
                    new AdjustmentLineItem { Key = "penalty", Label = "Penalty", HasAdjustmentColumn = true, IsChecked = false },
                    new AdjustmentLineItem { Key = "present", Label = "Present", HasAdjustmentColumn = false, IsChecked = false },
                    new AdjustmentLineItem { Key = "previous", Label = "Previous", HasAdjustmentColumn = false, IsChecked = false }
                }
            };

            return View(model);
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

        public async Task<IActionResult> GetBillByBillPeriod(DateTime billPeriod)
        {
            return Ok();
        }

        #endregion

    }
}
