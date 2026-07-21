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

        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var template = await _service.Get(id);
            if (template == null) return NotFound();
            return Ok(template);
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var templates = await _service.GetAll();
            if(templates == null || !templates.Any()) return NotFound();
            return Ok(templates);
        }

        [HttpGet("GetByUser/{userId}")]
        public async Task<IActionResult> GetByUser(int userId)
        {
            var templates = await _service.GetByUser(userId);
            if (templates == null || !templates.Any()) return NotFound();
            return Ok(templates);
        }

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<IActionResult> SaveUpdate(int userId, ReadingSheetTemplate template)
        {
            var savedTemplate = await _service.SaveUpdate(userId, template);
            return Ok(savedTemplate);
        }

        #endregion

    }
}
