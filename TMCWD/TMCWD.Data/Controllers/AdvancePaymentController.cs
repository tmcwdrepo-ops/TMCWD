using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdvancePaymentController : Controller
    {
        #region fields

        private readonly IAdvancePaymentService _service;

        #endregion

        #region constructors

        public AdvancePaymentController(IAdvancePaymentService service)
        {
            _service = service;
        }

        #endregion

        #region methods

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "Collection")]
        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            if (id == 0) return BadRequest();

            var advancePayment = await _service.Get(id);

            if (advancePayment == null) return NotFound();

            return Ok(advancePayment);

        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "Collection")]
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var advancePayments = await _service.GetAll();

            if (advancePayments == null || !advancePayments.Any()) return NotFound();

            return Ok(advancePayments);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "Collection")]
        [HttpGet("GetActiveByAccount/{accountId}")]
        public async Task<IActionResult> GetActiveByAccount(int accountId)
        {

            if(accountId == 0) return BadRequest();

            var advancePayment = await _service.GetActiveByAccount(accountId);

            if (advancePayment == null) return NotFound();

            return Ok(advancePayment);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "Collection")]
        [HttpGet("GetByAccount/{accountId}")]
        public async Task<IActionResult> GetByAccount(int accountId)
        {
            if(accountId == 0) return BadRequest();

            var advancePayment = await _service.GetByAccount(accountId);

            if(advancePayment == null) return NotFound();

            return Ok(advancePayment);

        }

        [HttpPost("SaveUpdate/{userId}")]
        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "Collection")]
        public async Task<IActionResult> SaveUpdate(int userId, AdvancePayment advancePayment)
        {
            if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out userId) || userId <= 0)
                return Unauthorized();
            var savedAdvancePayment = await _service.SaveUpdate(userId, advancePayment);
            return Ok(savedAdvancePayment);
        }

        #endregion
    }
}
