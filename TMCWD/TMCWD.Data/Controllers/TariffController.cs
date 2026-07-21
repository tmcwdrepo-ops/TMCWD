using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TariffController : Controller
    {
        #region fields

        private readonly ITariffService _service;

        #endregion

        #region constructors

        public TariffController(ITariffService service)
        {
            _service = service;
        }

        #endregion

        #region methods

        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var tariff = await _service.Get(id);
            if (tariff == null) return NotFound();
            return Ok(tariff);
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var tariffs = await _service.GetAll();
            if(tariffs == null || !tariffs.Any()) return NotFound();
            return Ok(tariffs);
        }

        [HttpGet("GetByClassification/{classification}")]
        public async Task<IActionResult> GetByClassification(int classification)
        {
            var tariffs = await _service.GetByClassification(classification);
            if (tariffs == null || !tariffs.Any()) return NotFound();
            return Ok(tariffs);
        }

        #endregion

    }
}
