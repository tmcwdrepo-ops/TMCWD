using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
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
        private readonly MeterMaintenanceRateTransaction _meterMaintenanceRateTrans;
        private readonly PaymentTransaction _paymentTrans;
        private readonly BillingAdjustmentTransaction _billingAdjustmentTrans;
        private readonly DebitCreditMemoTransaction _debitCreditMemoTrans;
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
            WaterRateTransaction waterRateTrans,
            MeterMaintenanceRateTransaction meterMaintenanceRateTrans,
            PaymentTransaction paymentTrans,
            BillingAdjustmentTransaction billingAdjustmentTrans,
            DebitCreditMemoTransaction debitCreditMemoTrans)
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
            _meterMaintenanceRateTrans = meterMaintenanceRateTrans;
            _paymentTrans = paymentTrans;
            _billingAdjustmentTrans = billingAdjustmentTrans;
            _debitCreditMemoTrans = debitCreditMemoTrans;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (_user.User.Id <= 0)
            {
                context.Result = Request.Headers.Accept.Any(value =>
                        value?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true)
                    ? RedirectToAction("Index", "Home")
                    : Unauthorized();
                return;
            }

            base.OnActionExecuting(context);
        }

        [HttpGet]
        public async Task<IActionResult> AccountLedgerData(string accountNumber)
        {
            if (string.IsNullOrWhiteSpace(accountNumber) || accountNumber.Trim().Length > 50)
                return BadRequest(new { message = "A valid account number is required." });
            try
            {
                var ledger = await _billingTrans.GetAccountLedger(accountNumber.Trim());
                return ledger == null ? NotFound(new { message = "Account not found." }) : Json(ledger);
            }
            catch (HttpRequestException ex)
            {
                var status = ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : 502;
                return StatusCode(status, new { message = status is 401 or 403 ? "You are not authorized to view this account ledger." : "Unable to load the account ledger. Please try again." });
            }
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
            new AdjustmentLineItem { Key = "usage",       Label = "Usage",        HasAdjustmentColumn = false, IsChecked = false },
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

            var result = new List<object>();
            foreach (var billing in accountBillings)
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

                var present = reading?.CurrentReading ?? account.InitialReading;
                var previous = reading?.PreviousReading ?? account.InitialReading;
                var usage = Math.Max(0, present - previous);
                var payments = await _paymentTrans.GetPaymentHistoryByBillingReference(billing.BillingReferenceId);
                var successfulPayments = payments
                    .Where(payment => payment.Success)
                    .OrderByDescending(payment => payment.DateCreated)
                    .ToList();
                var latestPayment = successfulPayments.FirstOrDefault();
                var processor = latestPayment == null || latestPayment.CreatedBy <= 0
                    ? null
                    : await _userTrans.Get(Convert.ToInt32(latestPayment.CreatedBy));

                result.Add(new
                {
                    referenceNo = billing.BillingReferenceId,
                    billingDate = billing.BillingPeriod,
                    previous,
                    present,
                    usage,
                    amount = billing.TotalBillAmount,
                    debit = billing.TotalBillAmount,
                    credit = successfulPayments.Sum(payment => payment.PaidAmount),
                    paymentReference = latestPayment?.PaymentReference,
                    paymentDate = latestPayment?.DateCreated,
                    processedBy = processor?.Name,
                    status = billing.PaymentStatus.ToString()
                });
            }

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
                .Where(p => p.PaymentStatus == PaymentStatus.Unpaid || p.PaymentStatus == PaymentStatus.Overdue)
                .Sum(p => p.Amount);

            return Ok(new
            {
                currentBill = billing.TotalBillAmount,
                penalty = activePenaltyTotal
            });
        }

        /// <summary>
        /// Returns Debit/Credit Memo history for a given account number.
        /// Used by the Adjustments modal on the Customer Information page.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetDebitCreditMemosByAccount(string accountNumber)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
                return BadRequest("Account number is required.");

            var memos = await _debitCreditMemoTrans.GetByAccountNumber(accountNumber);
            return Ok(memos.OrderByDescending(m => m.DcmDate).Select(m => new
            {
                id            = m.Id,
                accountNumber = m.AccountNumber,
                dcmType       = m.DcmType,
                memoType      = m.MemoType,
                amount        = m.Amount,
                dcmDate       = m.DcmDate,
                billMonthBasis = m.BillMonthBasis,
                explanation   = m.Explanation,
                remarks       = m.Remarks,
                status        = m.Status,
                dateCreated   = m.DateCreated,
            }));
        }

        /// <summary>
        /// Returns Bill Adjustment history for a given account number.
        /// Walks account â†’ billings â†’ adjustments per billing reference.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetBillAdjustmentsByAccount(string accountNumber)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
                return BadRequest("Account number is required.");

            var account = await _accountTrans.GetByAccountNumber(accountNumber);
            if (account == null)
                return NotFound("Account not found.");

            var allBillings = await _billingTrans.GetAll() ?? new List<Model.Billing.Interfaces.BillingBase>();
            var accountBillings = allBillings
                .Where(b => b.AccountId == account.Id)
                .ToList();

            var result = new List<object>();

            foreach (var billing in accountBillings)
            {
                var adjustments = await _billingAdjustmentTrans.GetByReference(billing.BillingReferenceId)
                                  ?? new List<BillingAdjustment>();

                foreach (var adj in adjustments)
                {
                    var typeName = ((AdjustmentLineType)adj.Type).ToString();
                    result.Add(new
                    {
                        id               = adj.Id,
                        billingReference = adj.BillingReferenceId,
                        billingPeriod    = billing.BillingPeriod,
                        type             = typeName,
                        amount           = adj.Amount,
                        dateCreated      = adj.DateCreated,
                    });
                }
            }

            return Ok(result.OrderByDescending(r => ((dynamic)r).dateCreated));
        }

        /// <summary>
        /// GET /Billing/GetAdjustmentHistory?billingReferenceId=XXX
        ///
        /// Returns the read-only audit history for a billing reference as a JSON
        /// array, calling through to the Data API History endpoint.
        /// Protected by the existing MVC BillingOperations session check.
        /// Returns an empty array rather than 404 when no adjustments exist.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAdjustmentHistory(string billingReferenceId)
        {
            if (string.IsNullOrWhiteSpace(billingReferenceId))
                return BadRequest("A billing reference is required.");
            try
            {
                var history = await _billingAdjustmentTrans.GetHistory(billingReferenceId.Trim());
                return Ok(history.Select(h => new
                {
                    id                   = h.Id,
                    billingReferenceId   = h.BillingReferenceId,
                    serviceInvoiceNumber = h.ServiceInvoiceNumber,
                    adjustmentType       = h.AdjustmentType,
                    amount               = h.Amount,
                    reason               = h.Reason,
                    createdBy            = h.CreatedBy,
                    createdByName        = h.CreatedByName,
                    dateCreated          = h.DateCreated,
                    updatedBy            = h.UpdatedBy,
                    updatedByName        = h.UpdatedByName,
                    dateUpdated          = h.DateUpdated
                }));
            }
            catch (HttpRequestException)
            {
                return StatusCode(502, "Could not load adjustment history. Please try again.");
            }
        }

        /// <summary>Combines the dropdown category and free-text explanation into one stored reason.</summary>
        private static string? CombineReason(string? category, string? explanation)
        {
            var cat  = category?.Trim() ?? string.Empty;
            var expl = explanation?.Trim() ?? string.Empty;
            if (cat.Length == 0 && expl.Length == 0) return null;
            if (cat.Length == 0) return expl.Length > 255 ? expl[..255] : expl;
            if (expl.Length == 0) return cat;
            var combined = $"{cat}: {expl}";
            return combined.Length > 255 ? combined[..255] : combined;
        }

        public IActionResult Index() => View();
        public IActionResult PenaltyCharging()
        {
            if (!HasRole(UserRole.CustomerServiceOic, UserRole.SuperAdmin))
                return Forbid();
            return View();
        }

        [HttpGet]
[HttpGet]
public async Task<IActionResult> GetPenaltyChargeEntries(
    DateTime? billingPeriod,
    DateTime? dueDate,
    DateTime? postingDate)
{
    if (!ModelState.IsValid)
        return BadRequest("Invalid billing date.");

    try
    {
        return Ok(await _penaltyTrans.GetChargeEntries(
            billingPeriod,
            dueDate,
            postingDate));
    }
    catch (HttpRequestException)
    {
        return StatusCode(
            502,
            "Could not load overdue bills. Please try again.");
    }
}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostPenaltyCharges([FromBody] PostPenaltyChargesRequest request)
        {
            if (_user.User == null || _user.User.Id <= 0) return Unauthorized();
            if (!HasRole(UserRole.Billing, UserRole.BillingHead, UserRole.SuperAdmin, UserRole.CustomerRepresentative, UserRole.CustomerServiceOic)) return Forbid();
            if (!ModelState.IsValid || request == null) return BadRequest("Invalid penalty posting request.");
            try { return Ok(await _penaltyTrans.PostCharges(_user.User.Id, request)); }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
            catch (HttpRequestException) { return StatusCode(502, "Posting was not confirmed. Reload entries before retrying."); }
        }
        public IActionResult Penalty()
        {
            if (!HasRole(UserRole.CustomerRepresentative, UserRole.CustomerServiceOic, UserRole.SuperAdmin))
                return Forbid();
            ViewBag.CanSubmitWaiver = HasRole(UserRole.CustomerRepresentative, UserRole.SuperAdmin);
            ViewBag.CanReviewWaiver = HasRole(UserRole.CustomerServiceOic, UserRole.SuperAdmin);
            return View();
        }

        private bool HasRole(params UserRole[] roles)
            => _user.User?.Role != null && roles.Select(x => (int)x).Contains(_user.User.Role.Value);

        public IActionResult Reading() => View("Reading");

        [HttpGet]
        public IActionResult GetBillByBillPeriod(DateTime billPeriod)
        {
            return StatusCode(501, "Bill adjustment submission is not available yet. No changes were saved.");
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
            //    var activePenalties = penalties.Where(p => p.PaymentStatus == PaymentStatus.Unpaid || p.PaymentStatus == PaymentStatus.Overdue).ToList();

            //    if (!activePenalties.Any()) continue; // only accounts with active penalty records

            //    if(billing.AccountId <= 0) continue;
            //    var account = await _accountTrans.Get((int)billing.AccountId);

            //    result.Add(new
            //    {
            //        billingReferenceId = billing.BillingReferenceId,
            //        accountNumber = account?.AccountNumber ?? "Ã¢â‚¬â€",
            //        usage = 0,          // not yet tracked Ã¢â‚¬â€ see note
            //        billAmount = billing.TotalBillAmount,
            //        discount = 0,       // not yet tracked Ã¢â‚¬â€ see note
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
            if (_user.User == null || _user.User.Id <= 0)
                return Unauthorized();

            if (model == null || string.IsNullOrWhiteSpace(model.ReferenceNo))
                return BadRequest("Select a billing record before submitting an adjustment.");
            if (model.AdjustmentOperationId == Guid.Empty)
                return BadRequest("A stable adjustment operation ID is required.");

            var selectedLines = model.AdjustmentLines?
                .Where(line => line.IsChecked)
                .ToList() ?? new List<AdjustmentLineItem>();
            var monetaryLines = selectedLines
                .Where(line => line.HasAdjustmentColumn)
                .ToList();

            if (monetaryLines.Count == 0)
                return BadRequest("Select at least one bill or penalty amount to adjust.");
            if (monetaryLines.Any(line => !line.AsBilled.HasValue || !line.ShouldBe.HasValue))
                return BadRequest("As Billed and Should Be are required for each monetary adjustment.");

            var billing = (await _billingTrans.GetAll() ?? new List<Model.Billing.Interfaces.BillingBase>())
                .FirstOrDefault(item => string.Equals(item.BillingReferenceId, model.ReferenceNo, StringComparison.OrdinalIgnoreCase));
            if (billing == null)
                return BadRequest("The selected billing record no longer exists. Reload billing history.");
            // Data resolves successful retries before checking settled status under the billing row lock.

            try
            {
                var adjustments = new List<BillingAdjustment>();
                foreach (var line in monetaryLines)
                {
                    if (!Enum.TryParse<AdjustmentLineType>(line.Key, true, out var type))
                        return BadRequest("The selected adjustment type is invalid.");

                    var amount = line.AsBilled!.Value - line.ShouldBe!.Value;
                    if (amount == 0)
                        return BadRequest("Each selected adjustment must change the billed amount.");

                    adjustments.Add(new BillingAdjustment
                    {
                        Type = (int)type,
                        BillingReferenceId = billing.BillingReferenceId,
                        Amount = amount,
                        // Combine the dropdown category and free-text explanation into the
                        // persisted Reason field. Either component may be absent.
                        Reason = CombineReason(model.Remarks, model.Explanation)
                    });
                }

                var saved = await _billingAdjustmentTrans.SaveBatch(_user.User.Id, adjustments, model.AdjustmentOperationId);
                if (saved == null || saved.Count != adjustments.Count)
                    return StatusCode(502, "The billing adjustment was not saved. No changes were confirmed.");

                TempData["SuccessMessage"] = $"{monetaryLines.Count} billing adjustment(s) saved successfully.";
                TempData["CompletedAdjustmentOperationId"] = model.AdjustmentOperationId.ToString("D");
                return RedirectToAction(nameof(BillAdjustment));
            }
            catch (HttpRequestException error)
            {
                return StatusCode((int)(error.StatusCode ?? System.Net.HttpStatusCode.BadGateway),
                    "The adjustment was not confirmed. Keep the same operation identity when retrying.");
            }
        }

        public async Task<IActionResult> GetBillById(int id)
        {
            return Ok();
        }
        public class WaivePenaltiesRequest
        {
            public List<string> BillingReferenceIds { get; set; } = new();
            public string Reason { get; set; } = string.Empty;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> WaivePenalties([FromBody] WaivePenaltiesRequest request)
        {
            if (!HasRole(UserRole.CustomerRepresentative, UserRole.SuperAdmin)) return Forbid();
            if (request?.BillingReferenceIds == null || request.BillingReferenceIds.Count == 0
                || request.BillingReferenceIds.Count > 1000
                || request.BillingReferenceIds.Any(string.IsNullOrWhiteSpace))
                return BadRequest("Select valid billing references.");
            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 3 || request.Reason.Trim().Length > 500)
                return BadRequest("Enter a waiver reason between 3 and 500 characters.");

            try
            {
                var count = await _penaltyTrans.SubmitWaiverRequests(_user.User.Id, new SubmitPenaltyWaiverRequest
                {
                    BillingReferenceIds = request.BillingReferenceIds,
                    Reason = request.Reason.Trim()
                });
                return Ok(new { submittedCount = count });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch (HttpRequestException)
            {
                return StatusCode(502, "The penalty service did not confirm the request. Please retry.");
            }
        }

        [HttpGet]
        public async Task<IActionResult> PendingPenaltyWaivers()
        {
            if (!HasRole(UserRole.CustomerServiceOic, UserRole.SuperAdmin)) return Forbid();
            try { return Ok(await _penaltyTrans.GetPendingWaiverRequests(_user.User.Id)); }
            catch (HttpRequestException) { return StatusCode(502, "Could not load pending waiver requests."); }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewPenaltyWaiver([FromBody] ReviewPenaltyWaiverRequest request)
        {
            if (!HasRole(UserRole.CustomerServiceOic, UserRole.SuperAdmin)) return Forbid();
            if (request == null || request.RequestId <= 0 || (request.Remarks?.Length ?? 0) > 500)
                return BadRequest("Invalid waiver review.");
            try
            {
                var waivedCount = await _penaltyTrans.ReviewWaiverRequest(_user.User.Id, request);
                return Ok(new { approved = request.Approve, waivedCount });
            }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
            catch (HttpRequestException) { return StatusCode(502, "The penalty review was not confirmed."); }
        }

        public IActionResult DebitCreditMemo()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDebitCreditMemo([FromBody] DebitCreditMemo memo)
        {
            if (_user.User == null || _user.User.Id <= 0)
                return Unauthorized();

            if (memo == null || string.IsNullOrWhiteSpace(memo.AccountNumber)
                || string.IsNullOrWhiteSpace(memo.DcmType)
                || (memo.MemoType != "debit" && memo.MemoType != "credit")
                || memo.Amount <= 0 || string.IsNullOrWhiteSpace(memo.Explanation))
                return BadRequest("A valid account, memo type, amount, and explanation are required.");

            var account = await _accountTrans.GetByAccountNumber(memo.AccountNumber.Trim());
            if (account == null)
                return BadRequest("The selected account no longer exists.");

            memo.AccountNumber = account.AccountNumber;
            memo.DcmType = memo.DcmType.Trim();
            memo.Explanation = memo.Explanation.Trim();
            memo.Remarks = memo.Remarks?.Trim() ?? string.Empty;
            memo.DcmDate = memo.DcmDate == default ? DateTime.Today : memo.DcmDate.Date;
            memo.BillMonthBasis = memo.BillMonthBasis?.Date;

            try
            {
                var saved = await _debitCreditMemoTrans.Save(_user.User.Id, memo);
                if (saved == null)
                    return StatusCode(502, "The debit/credit memo was not saved. Please try again.");
                return Ok(new { id = saved.Id, accountNumber = saved.AccountNumber, amount = saved.Amount, memoType = saved.MemoType });
            }
            catch (HttpRequestException)
            {
                return StatusCode(502, "The debit/credit memo was not saved. Please try again.");
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetChargeTypes()
        {
            var types = await _otherFeeTypeTrans.GetAll() ?? new List<OtherFeeType>();
            if (types.Count > 0)
            {
                return Ok(types.Select(t => new
                {
                    value = t.Id,
                    label = t.Name,
                    defaultAmount = t.DefaultValue
                }));
            }

            // Keep every Other Charges entry point usable before master charge
            // types have been configured in the database.
            return Ok(new[]
            {
                new { value = 4L, label = "MSR - Recon. Fee", defaultAmount = 0m },
                new { value = 1L, label = "Reconnection Fee", defaultAmount = 350m },
                new { value = 2L, label = "Penalty", defaultAmount = 0m },
                new { value = 3L, label = "Meter Testing Fee", defaultAmount = 0m },
                new { value = 5L, label = "Service Connection Fee", defaultAmount = 0m },
                new { value = 6L, label = "Miscellaneous Charge", defaultAmount = 0m }
            });
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
                List<Model.Billing.OtherCharge> charges;
                CollectionBill? summary;
                try
                {
                    summary = await _billingTrans.GetCollectionBill(billing.BillingReferenceId);
                    charges = await _otherChargeTrans.GetByReference(billing.BillingReferenceId) ?? new();
                }
                catch (HttpRequestException error) when (error.StatusCode == System.Net.HttpStatusCode.Conflict)
                { return Conflict(new { message = "An Other Charge has an invalid installment ledger. Contact the billing administrator." }); }
                var activeCharges = charges.Where(c => c.IsActive
                    && c.PaymentStatus != PaymentStatus.Paid
                    && c.PaymentStatus != PaymentStatus.Waived).ToList();

                foreach (var charge in activeCharges)
                {
                    var feeType = await _otherFeeTypeTrans.Get(charge.Type);
                    result.Add(new
                    {
                        id = charge.Id,
                        description = feeType?.Name ?? "â€”",
                        payableIn = charge.PayableIn,
                        particulars = charge.Particulars,
                        amount = charge.Amount,
                        installmentCount = charge.InstallmentCount,
                        installments = charge.Installments,
                        collectibleAmount = summary?.CollectibleOtherCharges.Where(c => c.Id == charge.Id).Sum(c => c.Amount) ?? 0m,
                        isPosted = charge.PaymentStatus != PaymentStatus.Draft,
                        status = charge.PaymentStatus.ToString()
                    });
                }
            }

            return Ok(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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
            int installmentCount;
            try
            {
                installmentCount = OtherChargeInstallments.ParseCount(request.PayableIn);
                _ = OtherChargeInstallments.BaseAmount(request.Amount, installmentCount);
            }
            catch (InvalidOperationException error) { return BadRequest(error.Message); }

            var account = await _accountTrans.GetByAccountNumber(request.AccountNumber);
            if (account == null)
                return NotFound("Account not found.");

            var allBillings = await _billingTrans.GetAll() ?? new List<Model.Billing.Interfaces.BillingBase>();
            var currentBilling = allBillings
                .Where(b => b.AccountId == account.Id
                    && (installmentCount > 1 ? b.ReadingId > 0
                        : b.PaymentStatus != PaymentStatus.Paid && b.PaymentStatus != PaymentStatus.Waived))
                .OrderByDescending(b => b.DateCreated)
                .FirstOrDefault();

            if (currentBilling == null)
            {
                if (installmentCount > 1)
                    return BadRequest("An installment charge requires an existing billing period.");
                currentBilling = await _billingTrans.CreateChargeAnchor(account.Id);

                if (currentBilling == null)
                    return BadRequest("Could not create a billing record for this account.");
            }

            var otherCharge = new Model.Billing.OtherCharge
            {
                BillingReferenceId = currentBilling.BillingReferenceId,
                Type = request.ChargeType,
                Amount = request.Amount,
                PayableIn = request.PayableIn,
                Particulars = request.Particulars,
                PaymentStatus = PaymentStatus.Draft,
                IsActive = true,
                DateCreated = DateTime.Now,
                DateUpdated = DateTime.Now
            };

            Model.Billing.OtherCharge saved;
            try
            {
                saved = await _otherChargeTrans.SaveUpdate(_user.User.Id, otherCharge);
            }
            catch (InvalidOperationException error) { return BadRequest(error.Message); }
            catch (HttpRequestException error) when (error.StatusCode == System.Net.HttpStatusCode.Conflict)
            { return Conflict(error.Message); }
            catch (Exception) { return StatusCode(500, "The charge could not be saved. Try again."); }

            if (saved == null)
            {
                return BadRequest("OtherChargeTransaction.SaveUpdate returned null â€” the API call to TMCWD.Data likely failed. Check TMCWD.Data is running and reachable.");
            }

            var feeType = await _otherFeeTypeTrans.Get(request.ChargeType);

            return Ok(new
            {
                id = saved.Id,
                description = feeType?.Name ?? "â€”",
                payableIn = saved.PayableIn,
                particulars = saved.Particulars,
                amount = saved.Amount,
                installmentCount = saved.InstallmentCount,
                collectibleAmount = 0m,
                isPosted = false,
                status = PaymentStatus.Draft.ToString()
            });
        }

       

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostOtherCharges([FromBody] PostOtherChargesRequest request)
        {
            if (request?.OtherChargeIds == null || request.OtherChargeIds.Count == 0)
                return BadRequest(new { message = "Select at least one draft charge to post." });

            try
            {
                var postedCount = await _otherChargeTrans.PostToCollection(_user.User.Id, request);
                return Ok(new { postedCount });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (HttpRequestException)
            {
                return StatusCode(502, new { message = "The charges were not posted. Please try again." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateOtherCharge(int id)
        {
            var result = await _otherChargeTrans.Deactivate(id, _user.User.Id);
            if (result == null)
                return NotFound();

            return Ok(true);
        }

        [HttpGet]
        public async Task<IActionResult> GetReadingsByZoneBook(int zone, int book, int readingSheetId)
        {
            if (zone <= 0 || book <= 0)
                return BadRequest("Zone and book are required.");
            if (readingSheetId <= 0)
                return BadRequest(new { message = "An active reading sheet is required for a complete bill estimate." });
            var sheets = await _readingSheetTrans.GetByZoneAndBook(zone, book);
            var readingSheet = sheets?.SingleOrDefault(sheet => sheet.Id == readingSheetId
                && (sheet.Status == ReadingStatus.Created || sheet.Status == ReadingStatus.InProgress));
            if (readingSheet?.BillingDate == null)
                return BadRequest(new { message = "The selected active reading sheet or its billing date was not found." });
            MeterMaintenanceRateDto? maintenance;
            try
            {
                maintenance = await _meterMaintenanceRateTrans.GetForBillingDate(
                    DateOnly.FromDateTime(readingSheet.BillingDate.Value.Date));
            }
            catch (InvalidOperationException error)
            {
                return Conflict(new { message = error.Message });
            }
            catch (HttpRequestException)
            {
                return StatusCode(502, new { message = "The Meter Maintenance configuration could not be loaded." });
            }
            if (maintenance == null)
                return Conflict(new { message = "No Meter Maintenance rate is configured for this billing date. Posting and complete estimates are blocked." });

            // Load all accounts for the zone/book and their readings in parallel
            var accountsTask = _accountTrans.GetByZoneAndBook(zone, book);
            var readingsTask = _readingTrans.GetByZoneAndBook(zone, book);
            await Task.WhenAll(accountsTask, readingsTask);

            var accounts = accountsTask.Result?
                .Where(account => account.Status == AccountStatus.Active)
                .ToList();
            var readings = readingsTask.Result ?? new List<TMCWD.Model.Billing.Reading>();

            if (accounts == null || !accounts.Any())
                return Ok(new List<PresentReadingViewModel>());

// One customer may own multiple accounts.
// Fetch each unique customer only once so CustomerId remains unique in the dictionary.
var customerIds = accounts
    .Select(a => a.CustomerId)
    .Distinct()
    .ToList();

var customerTasks = customerIds
    .Select(customerId => _customerTrans.Get(customerId))
    .ToArray();

var customers = await Task.WhenAll(customerTasks);

var customerById = customerIds
    .Select((customerId, index) => new
    {
        CustomerId = customerId,
        Customer = customers[index]
    })
    .Where(x => x.Customer != null)
    .ToDictionary(
        x => x.CustomerId,
        x => x.Customer!
    );

            // Group readings by AccountId Ã¢â‚¬â€ keep only the most recent per account
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
                    amount = await WaterChargeCalculator.ComputeBillAmount(
                        _waterRateTrans,
                        (int)previous,
                        (int)present,
                        account.Classification,
                        account.MeterSize > 0 ? account.MeterSize : 0.5m,
                        maintenance.Amount);
                }

                var customerName = customerById.TryGetValue(account.CustomerId, out var cust) && cust != null
                    ? cust.FullName.Trim().TrimEnd(',')
                    : account.AccountNumber;

                result.Add(new PresentReadingViewModel
                {
                    ReadingId = reading?.Id ?? 0,
                    AccountId = account.Id,
                    AccountNumber = account.AccountNumber,
                    Name = customerName,
                    Classification = account.Classification.ToString(),
                    MeterNumber = account.MeterNumber,
                    Address = account.FullAddress,
                    PresentReading = present,
                    PreviousReading = previous,
                    Amount = amount,
                    MeterMaintenanceFee = maintenance.Amount,
                    MeterSize = account.MeterSize
                });
            }

            return Ok(result);
        }



        /// <summary>
        /// Live search Ã¢â‚¬â€ returns matching accounts (by account number, meter number, or address)
        /// together with each account's most recent reading. Used to populate the table as the
        /// user types in the Account Number field.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SearchAccounts(string q, int? readingSheetId)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 1)
                return Ok(new List<object>());

            var accounts = (await _accountTrans.Search(q.Trim()))?
                .Where(account => account.Status == AccountStatus.Active)
                .ToList();
            if (accounts == null || !accounts.Any())
                return Ok(new List<object>());
            MeterMaintenanceRateDto? maintenance = null;
            if (readingSheetId.HasValue)
            {
                var sheet = await _readingSheetTrans.Get(readingSheetId.Value);
                if (sheet?.BillingDate == null
                    || sheet.Status is not (ReadingStatus.Created or ReadingStatus.InProgress))
                    return BadRequest(new { message = "An active reading sheet with a billing date is required for a complete bill estimate." });
                try
                {
                    maintenance = await _meterMaintenanceRateTrans.GetForBillingDate(
                        DateOnly.FromDateTime(sheet.BillingDate.Value.Date));
                }
                catch (InvalidOperationException error)
                {
                    return Conflict(new { message = error.Message });
                }
                catch (HttpRequestException)
                {
                    return StatusCode(502, new { message = "The Meter Maintenance configuration could not be loaded." });
                }
                if (maintenance == null)
                    return Conflict(new { message = "No Meter Maintenance rate is configured for this billing date." });
            }

            // Fetch all readings in parallel Ã¢â‚¬â€ one Task per account instead of
            // two sequential awaits per account inside a foreach loop.
            var readingTasks = accounts.Select(a => _readingTrans.GetByAccount(a.Id));
            var allReadings = await Task.WhenAll(readingTasks);

            // Fetch customer names for all matched accounts in parallel
            var customerTasks = accounts.Select(a => _customerTrans.Get(a.CustomerId));
            var allCustomers = await Task.WhenAll(customerTasks);

            var result = new List<object>();

            for (int i = 0; i < accounts.Count; i++)
            {
                var account = accounts[i];
                var readings = allReadings[i];
                var customer = allCustomers[i];

                var customerName = customer != null
                    ? customer.FullName.Trim().TrimEnd(',')
                    : account.AccountNumber;

                var current = readings?
                    .OrderByDescending(r => r.Id)
                    .FirstOrDefault();

                var present = current?.CurrentReading ?? account.InitialReading;
                var previousVal = current?.PreviousReading ?? account.InitialReading;

                decimal amount = 0;

                if (current != null)
                {
                    amount = await WaterChargeCalculator.ComputeBillAmount(
                        _waterRateTrans,
                        (int)previousVal,
                        (int)present,
                        account.Classification,
                        account.MeterSize > 0 ? account.MeterSize : 0.5m,
                        maintenance?.Amount ?? 0m);
                }

                var usage = Math.Max(0, present - previousVal);

                result.Add(new
                {
                    accountId = account.Id,
                    accountNumber = account.AccountNumber,
                    name = customerName,
                    meterNumber = account.MeterNumber,
                    address = account.FullAddress,
                    classification = account.Classification.ToString(),
                    meterSize = account.MeterSize,
                    presentReading = present,
                    previousReading = previousVal,
                    usage,
                    amount,
                    meterMaintenanceFee = maintenance?.Amount
                });
            }

            return Ok(result);
        }

        /// <summary>
        /// Returns all reading records for a given account number.
        /// Used to populate the Present Reading table when looking up by account.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetReadingsByAccount(string accountNumber, int? readingSheetId)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
                return BadRequest("Account number is required.");

            var account = await _accountTrans.GetByAccountNumber(accountNumber);
            if (account == null)
                return NotFound("Account not found.");
            if (account.Status != AccountStatus.Active)
                return Conflict("This account is not active yet. Complete the connection and House Tapping workflow before recording meter readings.");

            var readings = await _readingTrans.GetByAccount(account.Id);
            MeterMaintenanceRateDto? maintenance = null;
            if (readingSheetId.HasValue)
            {
                var sheet = await _readingSheetTrans.Get(readingSheetId.Value);
                if (sheet?.BillingDate == null
                    || sheet.Status is not (ReadingStatus.Created or ReadingStatus.InProgress))
                    return BadRequest(new { message = "An active reading sheet with a billing date is required for a complete bill estimate." });
                try
                {
                    maintenance = await _meterMaintenanceRateTrans.GetForBillingDate(
                        DateOnly.FromDateTime(sheet.BillingDate.Value.Date));
                }
                catch (InvalidOperationException error)
                {
                    return Conflict(new { message = error.Message });
                }
                catch (HttpRequestException)
                {
                    return StatusCode(502, new { message = "The Meter Maintenance configuration could not be loaded." });
                }
                if (maintenance == null)
                    return Conflict(new { message = "No Meter Maintenance rate is configured for this billing date." });
            }

            if (readings == null || !readings.Any())
                return Ok(new List<PresentReadingViewModel>());

            var result = new List<PresentReadingViewModel>();
            foreach (var reading in readings)
            {
                var usage = Math.Max(
                    0,
                    reading.CurrentReading - reading.PreviousReading);

                var amount = maintenance == null
                    ? await WaterChargeCalculator.ComputeWaterCharge(
                    _waterRateTrans,
                    (int)reading.PreviousReading,
                    (int)reading.CurrentReading,
                    account.Classification,
                    account.MeterSize > 0 ? account.MeterSize : 0.5m)
                    : await WaterChargeCalculator.ComputeBillAmount(
                        _waterRateTrans,
                        (int)reading.PreviousReading,
                        (int)reading.CurrentReading,
                        account.Classification,
                        account.MeterSize > 0 ? account.MeterSize : 0.5m,
                        maintenance.Amount);
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
                    MeterMaintenanceFee = maintenance?.Amount,
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
            if (account.Status != AccountStatus.Active)
                return Conflict("This account is not active yet. Complete the connection and House Tapping workflow before recording meter readings.");

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
                PreviousReading = currentReading?.CurrentReading ?? account.InitialReading,
                MeterSize = account.MeterSize
            };

            return Ok(result);
        }

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

                if (request.ReadingSheetId <= 0)
                    return BadRequest("An active reading sheet is required.");

                // ============================================================
                // 1. GET ACCOUNT
                // ============================================================

                var account = await _accountTrans.GetByAccountNumber(request.AccountNumber);

                if (account == null)
                    return NotFound("Account not found.");
                if (account.Status != AccountStatus.Active)
                    return Conflict("This account is not active yet. Complete the connection and House Tapping workflow before recording meter readings.");

                // ============================================================
                // 2. GET EXISTING READING SHEET
                // ============================================================

                var existingSheets =
                    await _readingSheetTrans.GetByZoneAndBook(request.Zone, request.Book);

                var readingSheet = existingSheets?
                    .Where(s => s.Id == request.ReadingSheetId
                             && s.AssignedTo == request.ReaderId
                             && (s.Status == ReadingStatus.Created
                                 || s.Status == ReadingStatus.InProgress))
                    .FirstOrDefault();

                if (readingSheet == null)
                    return BadRequest("The selected reading sheet is not active or does not match the zone, book, and meter reader.");

                if (account.ZoneBookId != readingSheet.ZoneBookId)
                    return BadRequest("The account does not belong to the selected reading sheet.");

                if (!readingSheet.BillingPeriodStart.HasValue
                    || !readingSheet.BillingDate.HasValue
                    || !readingSheet.DueDate.HasValue)
                    return BadRequest("The reading sheet schedule is incomplete. Reading, billing, and due dates are required.");

                if (request.BillingDate.Date != readingSheet.BillingDate.Value.Date
                    || request.DueDate.Date != readingSheet.DueDate.Value.Date)
                    return BadRequest("The billing and due dates do not match the selected reading sheet.");

                if (request.ReadingDate.Date >= readingSheet.BillingDate.Value.Date)
                    return BadRequest("The reading date must be before the reading sheet billing date.");

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

                if (accountReadings?.Any(r => r.ReadingSheetId == readingSheet.Id) == true)
                    return Conflict("A reading already exists for this account and reading sheet. Reload the sheet before continuing.");

                var latestReading = accountReadings?
                    .OrderByDescending(r => r.Id)
                    .FirstOrDefault();

                decimal previousReading =
                    latestReading?.CurrentReading ?? account.InitialReading;

                if (request.PresentReading < previousReading)
                    return BadRequest("Present reading cannot be less than the previous reading.");

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

                    // Manual reading date selected by the user.
                    DateCreated = request.ReadingDate.Date,

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
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
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
        public async Task<IActionResult> GetPenaltiesByDueDate(DateTime dueDate)
        {
            if (!ModelState.IsValid) return BadRequest("Invalid due date.");
            try { return Ok(await _penaltyTrans.GetActiveEntries(dueDate)); }
            catch (HttpRequestException) { return StatusCode(502, "Could not load penalties. Please try again."); }
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
                var activePenalties = penalties.Where(p => p.PaymentStatus == PaymentStatus.Unpaid || p.PaymentStatus == PaymentStatus.Overdue).ToList();

                if (!activePenalties.Any()) continue;
                if (billing.AccountId <= 0) continue;

                var account = await _accountTrans.Get((int)billing.AccountId);

                result.Add(new
                {
                    billingReferenceId = billing.BillingReferenceId,
                    accountNumber = account?.AccountNumber ?? "Ã¢â‚¬â€",
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

