using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class ReadingSheetTemplateController : Controller
    {
        #region fields

        private readonly IReadingSheetTemplateService _service;

        #endregion

        #region constructors

        public ReadingSheetTemplateController(IReadingSheetTemplateService service)
        {
            _service = service;
        }

        #endregion

        #region methods

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var template = await _service.Get(id);
            if (template == null) return NotFound();
            return Ok(template);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var templates = await _service.GetAll();
            if (templates == null || !templates.Any()) return NotFound();
            return Ok(templates);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetByUser/{userId}")]
        public async Task<IActionResult> GetByUser(int userId)
        {
            var templates = await _service.GetByUser(userId);
            if (templates == null || !templates.Any()) return NotFound();
            return Ok(templates);
        }

        [HttpPost("SaveUpdate/{userId}")]
        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        public async Task<IActionResult> SaveUpdate(int userId, ReadingSheetTemplate template)
        {
            if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out userId) || userId <= 0)
                return Unauthorized();
            try { return Ok(await _service.SaveUpdate(userId, template)); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPut("Deactivate/{id}/{userId}")]
        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        public async Task<IActionResult> Deactivate(int id, int userId)
        {
            if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out userId) || userId <= 0)
                return Unauthorized();
            var template = await _service.Deactivate(id, userId);

            if (template == null)
                return NotFound();

            return Ok(template);
        }

        #endregion

    }
}
