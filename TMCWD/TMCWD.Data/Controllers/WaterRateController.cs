using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WaterRateController : ControllerBase
    {
        private readonly IWaterRateService _service;

        public WaterRateController(IWaterRateService service)
        {
            _service = service;
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var rate = await _service.Get(id);

            if (rate == null)
                return NotFound();

            return Ok(rate);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var rates = await _service.GetAll();

            return Ok(rates);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetByClassification/{classification}")]
        public async Task<IActionResult> GetByClassification(int classification)
        {
            var rates = await _service.GetByClassification(classification);

            return Ok(rates);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetByClassificationAndMeterSize/{classification}/{meterSize}")]
         public async Task<IActionResult> GetByClassificationAndMeterSize(
         int classification,
         decimal meterSize)
        {
            var waterRate = await _service.GetByClassificationAndMeterSize(
                classification,
                meterSize);

            if (waterRate == null)
                return NotFound();

            return Ok(waterRate);
        }
    }
}
