using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;
using TMCWD.Model.Billing.Interfaces;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
    public class PenaltyController : Controller
    {

        #region fields

        private readonly IPenaltyService _service;

        #endregion

        #region constructors

        public PenaltyController(IPenaltyService service)
        {
            _service = service;
        }

        #endregion

        [HttpPost("SubmitWaiver/{userId}")]
        public async Task<IActionResult> SubmitWaiver(int userId, [FromBody] TMCWD.Model.Billing.SubmitPenaltyWaiverRequest request)
        {
            userId = ActorId;
            if (userId <= 0) return Unauthorized();
            try { return Ok(await _service.SubmitWaiverRequests(userId, request)); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
        }

        [HttpGet("PendingWaivers/{userId}")]
        public async Task<IActionResult> PendingWaivers(int userId)
        {
            userId = ActorId;
            if (userId <= 0) return Unauthorized();
            try { return Ok(await _service.GetPendingWaiverRequests(userId)); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        [HttpPost("ReviewWaiver/{userId}")]
        public async Task<IActionResult> ReviewWaiver(int userId, [FromBody] TMCWD.Model.Billing.ReviewPenaltyWaiverRequest request)
        {
            userId = ActorId;
            if (userId <= 0) return Unauthorized();
            try { return Ok(await _service.ReviewWaiverRequest(userId, request)); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
        }

        #region methods

        [HttpGet("ActiveEntries")]
        public async Task<IActionResult> ActiveEntries(DateTime dueDate)
            => Ok(await _service.GetActiveEntries(dueDate));

        [HttpGet("ChargeEntries")]
        public async Task<IActionResult> ChargeEntries(
            DateTime? billingPeriod,
            DateTime? dueDate,
            DateTime? postingDate)
        {
            return Ok(await _service.GetChargeEntries(
                billingPeriod,
                dueDate,
                postingDate));
        }
        
        [HttpPost("PostCharges/{userId}")]
        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        public async Task<IActionResult> PostCharges(int userId, [FromBody] TMCWD.Model.Billing.PostPenaltyChargesRequest request)
        {
            try {
                userId = ActorId;
                if (userId <= 0) return Unauthorized();
                return Ok(await _service.PostCharges(userId, request));
            }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
        }

        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var penalty = await _service.Get(id);
            if (penalty == null) return NotFound();
            return Ok(penalty);
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var penalties = await _service.GetAll();
            if(penalties == null || !penalties.Any()) return NotFound();    
            return Ok(penalties);
        }

        [HttpGet("GetByReference/{reference}")]
        public async Task<IActionResult> GetByReference(string reference)
        {
            var penalties = await _service.GetByReference(reference);
            if(penalties == null || !penalties.Any()) return NotFound();
            return Ok(penalties);
        }

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<IActionResult> SaveUpdate(int userId, [FromBody] Penalty penalty)
        {
            userId = ActorId;
            if (userId <= 0) return Unauthorized();
            try { return Ok(await _service.SaveUpdate(userId, penalty)); }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        #endregion
        private int ActorId => int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    }
}
