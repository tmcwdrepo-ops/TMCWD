using Microsoft.AspNetCore.Mvc;
using TMCWD.Application.Models;
using TMCWD.Billing;
using TMCWD.CustomerSupport;
using TMCWD.Model.Administrator;
using TMCWD.Model.Billing;
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

        #endregion

        #region methods

        public CollectionsController(AuthenticatedUserService user,
            BillingTransaction billingTrans,
            PenaltyTransaction penaltyTrans,
            AccountTransaction accountTrans,
            CustomerTransaction customerTrans)
        {
            _user = user;
            _billingTrans = billingTrans;
            _penaltyTrans = penaltyTrans;
            _accountTrans = accountTrans;
            _customerTrans = customerTrans;
        }

        public IActionResult Index() => View("Collections");

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
                        .Where(p => p.PaymentStatus != PaymentStatus.Waived)
                        .ToList() ?? new List<Model.Billing.Penalty>();

                    var penaltyAmount = activePenalties.Sum(p => p.Amount);

                    // Calculate due date (15 days after billing period)
                    var dueDate = billing.BillingPeriod.AddDays(15);

                    totalBillAmount += billing.TotalBillAmount;
                    totalPenalty += penaltyAmount;

                    bills.Add(new
                    {
                        billingReferenceId = billing.BillingReferenceId,
                        billingPeriod = billing.BillingPeriod.ToString("yyyy-MM"),
                        dueDate = dueDate.ToString("yyyy-MM-dd"),
                        billAmount = billing.TotalBillAmount,
                        penalty = penaltyAmount,
                        status = billing.PaymentStatus.ToString()
                    });
                }

                result.Add(new
                {
                    accountId = account.Id,
                    accountNumber = account.AccountNumber,
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
