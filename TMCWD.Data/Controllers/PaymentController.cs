using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : Controller
    {

        #region fields

        private readonly IPaymentService _service;

        #endregion

        #region constructors

        public PaymentController(IPaymentService service)
        {
            _service = service;
        }

        #endregion

        #region methods

        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var payment = await _service.Get(id);
            if (payment == null) return NotFound();
            return Ok(payment);
        }

        [HttpGet("GetByBillingReference/{referenceId}")]
        public async Task<IActionResult> GetByBillingReference(string referenceId)
        {
            var payments = await _service.GetByBillingReference(referenceId);
            if(payments == null || !payments.Any()) return NotFound();
            return Ok(payments);
        }

        [HttpGet("GetByCashier/{userId}")]
        public async Task<IActionResult> GetByCashier(int userId)
        {
            var payments = await _service.GetByCashier(userId);
            if (payments == null || !payments.Any()) return NotFound();
            return Ok(payments);
        }

        [HttpGet("GetByMethod/{methodId}")]
        public async Task<IActionResult> GetByMethod(int methodId)
        {
            var payments = await _service.GetByMethod(methodId);
            if (payments == null || !payments.Any()) return NotFound();
            return Ok(payments);
        }

        [HttpGet("GetByMacAddress/{macAddress}")]
        public async Task<IActionResult> GetByMacAddress(string macAddress)
        {
            var payments = await _service.GetByMacAddress(macAddress);
            if (payments == null || !payments.Any()) return NotFound();
            return Ok(payments);
        }

        [HttpGet("GetByPaymentReference/{referenceId}")]
        public async Task<IActionResult> GetByPaymentReference(string referenceId)
        {
            var payment = await _service.GetByPaymentReference(referenceId);
            if (payment == null) return NotFound();
            return Ok(payment);
        }

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<IActionResult> SaveUpdate(int userId, Payment payment)
        {
            var savedPayment = await _service.SaveUpdate(userId, payment);
            return Ok(savedPayment);
        }

        #endregion

    }
}
