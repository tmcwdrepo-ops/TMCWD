using Microsoft.AspNetCore.Mvc;
using TMCWD.Application.Models;
using TMCWD.Billing;
using TMCWD.CustomerSupport;
using TMCWD.Model.Administrator;
using TMCWD.Model.Billing;
using TMCWD.Model.Billing.Requests;
using TMCWD.Services;

namespace TMCWD.Application.Controllers
{
    public class CollectionsController : Controller
    {
        #region constructors

        private readonly AuthenticatedUserService _user;
        private readonly BillingTransaction _billingTrans;
        private readonly PenaltyTransaction _penaltyTrans;
        private readonly AccountTransaction _accountTrans;
        private readonly CustomerTransaction _customerTrans;
        private readonly ReadingTransaction _readingTrans;
        private readonly ReadingSheetTransaction _readingSheetTrans;
        private readonly PaymentTransaction _paymentTrans;

        #endregion

        #region methods

        public CollectionsController(AuthenticatedUserService user,
            BillingTransaction billingTrans,
            PenaltyTransaction penaltyTrans,
            AccountTransaction accountTrans,
            CustomerTransaction customerTrans,
            ReadingTransaction readingTrans,
            ReadingSheetTransaction readingSheetTrans,
            PaymentTransaction paymentTrans)
        {
            _user = user;
            _billingTrans = billingTrans;
            _penaltyTrans = penaltyTrans;
            _accountTrans = accountTrans;
            _customerTrans = customerTrans;
            _readingTrans = readingTrans;
            _readingSheetTrans = readingSheetTrans;
            _paymentTrans = paymentTrans;
        }

        public IActionResult Index() => View("Collections");

        [HttpGet]
        public IActionResult CurrentCollector()
        {
            var user = _user.User;
            if (user == null || user.Id <= 0 || string.IsNullOrWhiteSpace(user.Name))
                return NoContent();

            return Ok(new
            {
                id = user.Id,
                name = user.Name
            });
        }

        [HttpPost]
        public async Task<IActionResult> Pay([FromBody] CollectionPaymentRequest request)
        {
            if (_user.User == null || _user.User.Id <= 0)
                return Unauthorized(new { success = false, message = "A valid cashier session is required." });
            if (request == null || !Enum.IsDefined(typeof(PaymentMethod), request.Method))
                return BadRequest(new { success = false, message = "Please select a payment method." });

            var result = await _paymentTrans.ProcessCollectionPayment(_user.User.Id, request);
            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true });
        }

        /// <summary>
        /// Search accounts for the Collections page with billing and penalty information.
        /// Returns account details, unpaid bills, and payment status.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SearchAccounts(string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
                return Ok(new List<object>());

            var accounts = await _accountTrans.Search(q.Trim());
            if (accounts == null || !accounts.Any())
                return Ok(new List<object>());

            var result = new List<object>();

            foreach (var account in accounts)
            {
                // Get customer information for the account name
                var customer = await _customerTrans.Get(account.CustomerId);
                var accountName = customer != null 
                    ? customer.FullName
                    : "Unknown Customer";

                // Get all billings for this account using GetByAccountId
                var billings = await _billingTrans.GetByAccountId(account.Id);

                // Filter unpaid bills
                var unpaidBills = billings?
                    .Where(b => b.PaymentStatus != PaymentStatus.Paid && b.PaymentStatus != PaymentStatus.Waived)
                    .OrderBy(b => b.BillingPeriod)
                    .ToList() ?? new List<Model.Billing.Interfaces.BillingBase>();

                // Calculate total amounts
                decimal totalBillAmount = 0;
                decimal totalPenalty = 0;
                var bills = new List<object>();

                foreach (var billing in unpaidBills)
                {
                    // Get penalties for this bill
                    var penalties = await _penaltyTrans.GetByReference(billing.BillingReferenceId);
                    var activePenalties = penalties?
                        .Where(p => p.PaymentStatus != PaymentStatus.Waived &&
                                    p.PaymentStatus != PaymentStatus.Paid)
                        .ToList() ?? new List<Model.Billing.Penalty>();

                    var penaltyAmount = activePenalties.Sum(p => p.Amount);

                    // Older records used their posting date as BillingPeriod.
                    // Follow the billing's reading back to its reading sheet so
                    // the scheduled billing month and due date are displayed.
                    Model.Billing.Reading? reading = null;
                    Model.Billing.ReadingSheet? readingSheet = null;
                    if (billing.ReadingId > 0)
                    {
                        reading = await _readingTrans.Get((int)billing.ReadingId);
                        if (reading?.ReadingSheetId > 0)
                            readingSheet = await _readingSheetTrans.Get(reading.ReadingSheetId);
                    }

                    var billingDate = readingSheet?.BillingDate ?? billing.BillingPeriod;
                    var dueDate = readingSheet?.DueDate ?? billingDate.AddDays(15);

                    totalBillAmount += billing.TotalBillAmount;
                    totalPenalty += penaltyAmount;

                    bills.Add(new
                    {
                        billingReferenceId = billing.BillingReferenceId,
                        readingSheetId = readingSheet?.Id,
                        billingPeriod = billingDate.ToString("yyyy-MM"),
                        dueDate = dueDate.ToString("yyyy-MM-dd"),
                        billAmount = billing.TotalBillAmount,
                        meterMaintenanceFee = WaterChargeCalculator.WaterMeterMaintenanceFee,
                        penalty = penaltyAmount,
                        status = billing.PaymentStatus.ToString()
                    });
                }

                result.Add(new
                {
                    accountId = account.Id,
                    accountNumber = account.AccountNumber,
                    meterNumber = account.MeterNumber,
                    classification = account.Classification.ToString(),
                    accountName,
                    address = $"{account.HouseNumber} {account.Street}, {account.Barangay}".Trim(),
                    totalBillAmount,
                    totalPenalty,
                    totalDue = totalBillAmount + totalPenalty,
                    unpaidCount = unpaidBills.Count,
                    bills
                });
            }

            return Ok(result);
        }

        #endregion
    }
}
