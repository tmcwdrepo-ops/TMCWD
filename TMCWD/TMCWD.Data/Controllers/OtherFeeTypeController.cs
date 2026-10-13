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

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var otherFeeType = await _service.Get(id);

            if (otherFeeType == null)
                return NotFound();

            return Ok(otherFeeType);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var otherFeeTypes = await _service.GetAll();

            if (otherFeeTypes == null || !otherFeeTypes.Any())
                return NotFound();

            return Ok(otherFeeTypes);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
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
        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "SuperAdmin")]
        public async Task<IActionResult> SaveUpdate(
            int userId,
            OtherFeeType otherFeeType)
        {
            if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out userId) || userId <= 0)
                return Unauthorized();
            var savedOtherFeeType =
                await _service.SaveUpdate(userId, otherFeeType);

            return Ok(savedOtherFeeType);
        }

        [HttpDelete("Delete/{id}")]
        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "SuperAdmin")]
        public async Task<IActionResult> Delete(int id)
        {
            if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var actorId) || actorId <= 0)
                return Unauthorized();
            var result = await _service.Delete(id);

            return Ok(result);
        }
    }
}
