using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{
    [ApiController()]
    [Route("api/[controller]")]
    public class EndpointController : Controller
    {

        #region fields

        private readonly IEndpointService _service;

        #endregion

        #region constructors

        public EndpointController(IEndpointService service)
        {
            _service = service;
        }

        #endregion

        #region methods

        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var endpoint = await _service.Get(id);
            if (endpoint == null) return NotFound();
            return Ok(endpoint);
        }

        [HttpGet("GetByType/{type}")]
        public async Task<IActionResult> GetByType(int type)
        {
            var endpoints = await _service.GetByType(type);
            if(endpoints == null || !endpoints.Any()) return NotFound();
            return Ok(endpoints);
        }

        [HttpGet("GetByTypeAndName/{type}/{name}")]
        public async Task<IActionResult> GetByTypeAndName(int type, string name)
        {
            var endpoint = await _service.GetByTypeAndName(type, name);
            if (endpoint == null) return NotFound();
            return Ok(endpoint);
        }

        #endregion

    }
}
