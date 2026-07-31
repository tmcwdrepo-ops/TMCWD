using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BillingAdjustmentController : Controller
    {
        private readonly IBillingAdjustmentService _service;

        public BillingAdjustmentController(IBillingAdjustmentService service)
        {
            _service = service;
        }

        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var adjustment = await _service.Get(id);
            if (adjustment == null) return NotFound();
            return Ok(adjustment);
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var adjustments = await _service.GetAll();
            if (adjustments == null || !adjustments.Any()) return NotFound();
            return Ok(adjustments);
        }

        [HttpGet("GetByReference/{reference}")]
        public async Task<IActionResult> GetByReference(string reference)
        {
            var adjustments = await _service.GetByReference(reference);
            if (adjustments == null || !adjustments.Any()) return NotFound();
            return Ok(adjustments);
        }

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<IActionResult> SaveUpdate(int userId, [FromBody] BillingAdjustment billingAdjustment)
        {
            var saved = await _service.SaveUpdate(userId, billingAdjustment);
            if (saved == null) return BadRequest();
            return Ok(saved);
        }
    }
}