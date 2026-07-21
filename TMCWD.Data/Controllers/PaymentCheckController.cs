using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentCheckController : Controller
    {
        #region fields

        private readonly IPaymentCheckService _service;

        #endregion

        #region constructors

        public PaymentCheckController(IPaymentCheckService service)
        {
            _service = service;
        }

        #endregion

        #region methods

        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            if (id == 0) return BadRequest();

            var paymentCheck = await _service.Get(id);

            if(paymentCheck == null) return NotFound();

            return Ok(paymentCheck);
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {

            var paymentChecks = await _service.GetAll();

            if (paymentChecks == null || !paymentChecks.Any()) return NotFound();

            return Ok(paymentChecks);

        }

        [HttpGet("GetByReference/{reference}")]
        public async Task<IActionResult> GetByReference(string reference)
        {
            if(String.IsNullOrEmpty(reference)) return BadRequest();
            
            var paymentCheck = await _service.GetByReference(reference);

            if(paymentCheck == null) return NotFound();

            return Ok(paymentCheck);
        }

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<IActionResult> SaveUpdate(int userId, PaymentCheck paymentCheck)
        {
            var savedPaymentCheck = await _service.SaveUpdate(userId, paymentCheck);
            return Ok(savedPaymentCheck);
        }

        #endregion
    }
}
