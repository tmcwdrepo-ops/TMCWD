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

        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var otherCharges = await _service.Get(id);
            if (otherCharges == null) return NotFound();
            return Ok(otherCharges);
        }

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

        [HttpGet("GetByReference/{reference}")]
        public async Task<IActionResult> GetByReference(string reference)
        {
            if (String.IsNullOrEmpty(reference)) return BadRequest();
            var otherCharges = await _service.GetByReference(reference);
            if (otherCharges == null || !otherCharges.Any()) return NotFound();
            return Ok(otherCharges);
        }

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<IActionResult> SaveUpdate(int userId, OtherCharge otherCharge)
        {
            var savedOtherCharge = await _service.SaveUpdate(userId, otherCharge);
            return Ok(savedOtherCharge);
        }

        #endregion

    }
}
