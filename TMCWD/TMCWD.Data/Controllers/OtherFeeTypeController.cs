using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OtherFeeTypeController : Controller
    {
        private readonly IOtherFeeTypeService _service;

        public OtherFeeTypeController(IOtherFeeTypeService service)
        {
            _service = service;
        }

        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var otherFeeType = await _service.Get(id);

            if (otherFeeType == null)
                return NotFound();

            return Ok(otherFeeType);
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var otherFeeTypes = await _service.GetAll();

            if (otherFeeTypes == null || !otherFeeTypes.Any())
                return NotFound();

            return Ok(otherFeeTypes);
        }

        [HttpGet("GetByName/{name}")]
        public async Task<IActionResult> GetByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest();

            var otherFeeTypes = await _service.GetByName(name);

            if (otherFeeTypes == null || !otherFeeTypes.Any())
                return NotFound();

            return Ok(otherFeeTypes);
        }

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<IActionResult> SaveUpdate(
            int userId,
            OtherFeeType otherFeeType)
        {
            var savedOtherFeeType =
                await _service.SaveUpdate(userId, otherFeeType);

            return Ok(savedOtherFeeType);
        }

        [HttpDelete("Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _service.Delete(id);

            return Ok(result);
        }
    }
}