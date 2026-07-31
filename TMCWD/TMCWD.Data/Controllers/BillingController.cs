using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class BillingController : Controller
    {

        private readonly IBillingService _billingService;

        public BillingController(IBillingService billingService)
        {
            _billingService = billingService;
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

    }
}
