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

        #region methods

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

        [HttpGet("SaveUpdate/{userId}")]
        public async Task<IActionResult> SaveUpdate(int userId, Penalty penalty)
        {
            var updatedPenalty = await _service.SaveUpdate(userId, penalty);
            if(updatedPenalty == null) return BadRequest();
            return Ok(updatedPenalty);
        }

        #endregion

    }
}