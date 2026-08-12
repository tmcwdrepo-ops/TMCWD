using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;
using BillingHistoryModel = TMCWD.Model.Billing.BillingHistory;
namespace TMCWD.Data.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class BillingController : Controller
    {

        private readonly IBillingService _billingService;
        private readonly IAccountService _accountService;
        private readonly IReadingService _readingService;

        public BillingController(
            IBillingService billingService,
            IAccountService accountService,
            IReadingService readingService)
        {
            _billingService = billingService;
            _accountService = accountService;
            _readingService = readingService;
        }

        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var billing = await _billingService.Get(id);
            if (billing == null)
            {
                return NotFound();
            }
            return Ok(billing);
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var billings = await _billingService.GetAll();
            if (billings == null || !billings.Any()) return NotFound();
            return Ok(billings);
        }

        [HttpGet("GetByAccountId/{accountId}")]
        public async Task<IActionResult> GetByAccountId(int accountId)
        {
            var billings = await _billingService.GetByAccountId(accountId);
            if (billings == null || !billings.Any()) return NotFound();
            return Ok(billings);
        }

        [HttpGet("GetByJobOrderId/{jobOrderId}")]
        public async Task<IActionResult> GetByJobOrderId(int jobOrderId)
        {
            var billings = await _billingService.GetByJobOrderId(jobOrderId);
            if (billings == null || !billings.Any()) return NotFound();
            return Ok(billings);
        }

        [HttpGet("GetByBillingReference/{billingReferenceId}")]
        public async Task<IActionResult> GetByBillingReference(string billingReferenceId)
        {
            var billing = await _billingService.GetByBillingReference(billingReferenceId);
            if (billing == null)
            {
                return NotFound();
            }
            return Ok(billing);
        }

        [HttpGet("GetPaymentTransactionId/{paymentTransactionId}")]
        public async Task<IActionResult> GetByPaymentTransactionId(string paymentTransactionId)
        {
            var billing = await _billingService.GetByPaymentTransactionId(paymentTransactionId);
            if (billing == null)
            {
                return NotFound();
            }
            return Ok(billing);

        }

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<IActionResult> SaveUpdate(int userId, [FromBody]Billing billing)
        {
            var savedBilling = await _billingService.SaveUpdate(userId, billing);
            return Ok(savedBilling);
        }

        [HttpGet("History/{accountNumber}")]
        public async Task<IActionResult> History(string accountNumber)
        {
            var account = await _accountService.GetByAccountNumber(accountNumber);

            if (account == null)
                return Ok(new List<BillingHistoryModel>());

            var billings = await _billingService.GetByAccountId((int)account.Id);

            var readings = await _readingService.GetByAccountWithBillingDate((int)account.Id);

            var result = new List<BillingHistoryModel>();

            foreach (var billing in billings.OrderByDescending(x => x.BillingPeriod))
            {
                var current = readings
                    .FirstOrDefault(x => x.BillingDate == billing.BillingPeriod);

                if (current == null)
                    continue;

                var previous = readings
                    .Where(x => x.BillingDate < billing.BillingPeriod)
                    .OrderByDescending(x => x.BillingDate)
                    .FirstOrDefault();

                decimal previousReading = previous?.CurrentReading ?? 0;

                result.Add(new BillingHistoryModel
                {
                    ReferenceNo = billing.BillingReferenceId,
                    BillingDate = billing.BillingPeriod,
                    Previous = previousReading,
                    Present = current.CurrentReading,
                    Usage = current.CurrentReading - previousReading,
                    Amount = billing.TotalBillAmount
                });
            }

            return Ok(result);
        }
    }
}
