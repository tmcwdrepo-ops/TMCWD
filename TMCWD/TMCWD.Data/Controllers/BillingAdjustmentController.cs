using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
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

        /// <summary>
        /// GET api/BillingAdjustment/History/{billingReferenceId}
        ///
        /// Returns the full audit history for a billing reference, newest first,
        /// with resolved actor names. Returns an empty array (not 404) when no
        /// adjustments exist — the caller needs to distinguish "no history yet"
        /// from "bill not found". Read-only; no financial mutation.
        /// </summary>
        [HttpGet("History/{billingReferenceId}")]
        public async Task<IActionResult> History(string billingReferenceId)
        {
            if (string.IsNullOrWhiteSpace(billingReferenceId))
                return BadRequest(new { message = "A billing reference is required." });
            var history = await _service.GetHistory(billingReferenceId.Trim());
            return Ok(history);
        }

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<IActionResult> SaveUpdate(int userId, [FromBody] BillingAdjustment billingAdjustment,
            [FromHeader(Name = "Idempotency-Key")] Guid operationId)
        {
            if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out userId) || userId <= 0)
                return Unauthorized();
            try { return Ok(await _service.SaveUpdate(userId, billingAdjustment, operationId)); }
            catch (KeyNotFoundException error) { return NotFound(new { message = error.Message }); }
            catch (BillingAdjustmentConflictException error) { return Conflict(new { message = error.Message }); }
            catch (InvalidOperationException error) { return BadRequest(new { message = error.Message }); }
        }

        [HttpPost("SaveBatch/{userId}")]
        public async Task<IActionResult> SaveBatch(int userId, [FromBody] List<BillingAdjustment> billingAdjustments,
            [FromHeader(Name = "Idempotency-Key")] Guid operationId)
        {
            try
            {
                if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out userId) || userId <= 0)
                    return Unauthorized();
                return Ok(await _service.SaveBatch(userId, billingAdjustments, operationId));
            }
            catch (KeyNotFoundException error) { return NotFound(new { message = error.Message }); }
            catch (BillingAdjustmentConflictException error) { return Conflict(new { message = error.Message }); }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
