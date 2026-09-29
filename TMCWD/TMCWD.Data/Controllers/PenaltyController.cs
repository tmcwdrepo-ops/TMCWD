using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;
using TMCWD.Model.Billing.Interfaces;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
            try { return Ok(await _service.SubmitWaiverRequests(userId, request)); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
        }

        [HttpGet("PendingWaivers/{userId}")]
        public async Task<IActionResult> PendingWaivers(int userId)
        {
            try { return Ok(await _service.GetPendingWaiverRequests(userId)); }
            catch (UnauthorizedAccessException) { return Forbid(); }
        }

        [HttpPost("ReviewWaiver/{userId}")]
        public async Task<IActionResult> ReviewWaiver(int userId, [FromBody] TMCWD.Model.Billing.ReviewPenaltyWaiverRequest request)
        {
            try { return Ok(await _service.ReviewWaiverRequest(userId, request)); }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return Conflict(ex.Message); }
        }

        #region methods

        [HttpGet("ChargeEntries")]
        public async Task<IActionResult> ChargeEntries(DateTime? billingPeriod)
            => Ok(await _service.GetChargeEntries(billingPeriod));

        [HttpPost("PostCharges/{userId}")]
        public async Task<IActionResult> PostCharges(int userId, [FromBody] TMCWD.Model.Billing.PostPenaltyChargesRequest request)
        {
            try { return Ok(await _service.PostCharges(userId, request)); }
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
            var updatedPenalty = await _service.SaveUpdate(userId, penalty);
            if (updatedPenalty == null) return BadRequest();
            return Ok(updatedPenalty);
        }

        #endregion

    }
}
