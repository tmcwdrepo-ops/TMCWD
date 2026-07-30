using Microsoft.AspNetCore.Mvc;
using TMCWD.Billing;
using TMCWD.CustomerSupport;
using TMCWD.Model.Administrator;
using TMCWD.Services;

namespace TMCWD.Application.Controllers
{
    public class BillingController : Controller
    {
        #region fields

        private readonly AuthenticatedUserService _user;
        private readonly BillingTransaction _billingTrans;
        private readonly PenaltyTransaction _penaltyTrans;
        private readonly AccountTransaction _accountTrans;

        #endregion

        #region constructors

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

        #endregion

        #region methods

        public IActionResult Index() => View();
        public IActionResult PenaltyCharging() => View();
        public IActionResult Penalty() => View();

        [HttpGet]
        public async Task<IActionResult> GetBillByBillPeriod(DateTime billPeriod)
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

        #endregion
    }
}