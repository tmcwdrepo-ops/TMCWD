using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/[controll]")]
    public class WebHookController : Controller
    {

        #region fields

        private readonly IWebHookService _service;

        #endregion

        #region constructors

        public WebHookController(IWebHookService service)
        {
            _service = service;
        }

        #endregion

        #region methods

        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            if (id == 0) return BadRequest();

            var webHook = await _service.Get(id);

            if(webHook == null) return NotFound();

            return Ok(webHook);
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var webHooks = await _service.GetAll();
            
            if(webHooks == null || !webHooks.Any()) return NotFound();

            return Ok(webHooks);
        }

        [HttpGet("SaveUpdate")]
        public async Task<IActionResult> SaveUpdate(WebHook webHook)
        {
            var savedWebHook = await _service.SaveUpdate(webHook);
            return Ok(savedWebHook);
        }

        #endregion


    }
}
