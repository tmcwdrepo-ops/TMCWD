using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OtherChargeController : Controller
    {

        #region fields

        private readonly IOtherChargeService _service;

        #endregion

        #region constructors

        public OtherChargeController(IOtherChargeService service)
        {
            _service = service;
        }

        #endregion

        #region methods

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var otherCharges = await _service.Get(id);
            if (otherCharges == null) return NotFound();
            return Ok(otherCharges);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var otherCharges = await _service.GetAll();

            if(otherCharges == null || !otherCharges.Any())
            {
                return NotFound();
            }

            return Ok(otherCharges);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetByReference/{reference}")]
        public async Task<IActionResult> GetByReference(string reference)
        {
            if (String.IsNullOrEmpty(reference)) return BadRequest();
            try
            {
                var otherCharges = await _service.GetByReference(reference);
                if (otherCharges == null || !otherCharges.Any()) return NotFound();
                return Ok(otherCharges);
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        [HttpPost("SaveUpdate/{userId}")]
        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        public async Task<IActionResult> SaveUpdate(int userId, OtherCharge otherCharge)
        {
            try
            {
                if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out userId) || userId <= 0)
                    return Unauthorized();
                if (otherCharge == null)
                    return BadRequest("Other charge is required.");

                var savedOtherCharge = await _service.SaveUpdate(userId, otherCharge);

                if (savedOtherCharge == null)
                    return BadRequest("Failed to save other charge.");

                return Ok(savedOtherCharge);
            }
            catch (TMCWD.Model.Billing.PaymentConflictException ex) { return Conflict(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception)
            {
                return StatusCode(500, new { message = "The charge could not be saved. Try again." });
            }
        }

        [HttpPut("Deactivate/{id}/{userId}")]
        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        public async Task<IActionResult> Deactivate(int id, int userId)
        {
            try
            {
                if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out userId) || userId <= 0)
                    return Unauthorized();
                var otherCharge = await _service.Deactivate(id, userId);
                if (otherCharge == null) return NotFound();
                return Ok(otherCharge);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpPost("PostToCollection/{userId}")]
        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        public async Task<IActionResult> PostToCollection(
            int userId,
            [FromBody] TMCWD.Model.Billing.Requests.PostOtherChargesRequest request)
        {
            try
            {
                if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out userId) || userId <= 0)
                    return Unauthorized();
                return Ok(new { postedCount = await _service.PostToCollection(userId, request?.OtherChargeIds ?? new()) });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpPost("ProcessPayment/{userId}")]
        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "Collection")]
        public async Task<IActionResult> ProcessPayment(
            int userId,
            [FromBody] TMCWD.Model.Billing.Requests.OtherChargePaymentRequest request)
        {
            try
            {
                if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out userId) || userId <= 0)
                    return Unauthorized();
                var invoiceNumber = await _service.ProcessPayment(userId, request);
                return Ok(new { success = true, invoiceNumber });
            }
            catch (TMCWD.Model.Billing.PaymentConflictException ex) { return Conflict(new { success = false, message = ex.Message }); }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
        #endregion

    }
}
