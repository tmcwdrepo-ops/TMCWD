using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class ReadingController : Controller
    {

        #region fields

        private readonly IReadingService _service;

        #endregion

        #region constructors

        public ReadingController(IReadingService service)
        {
            _service = service;
        }

        #endregion

        #region methods

        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var reading = await _service.Get(id);
            if (reading == null) return NotFound();
            return Ok(reading);
        }

        [HttpGet("GetByAccount/{accountId}")]
        public async Task<IActionResult> GetByAccount(int accountId)
        {
            var readings = await _service.GetByAccount(accountId);
            if (readings == null || !readings.Any()) return NotFound();
            return Ok(readings);
        }

        [HttpGet("GetByAccountAndBillingPeriod/{accountId}/{billingPeriod}")]
        public async Task<IActionResult> GetByAccountAndBillingPeriod(int accountId, DateTime billingPeriod)
        {
            var reading = await _service.GetByAccountAndBillingPeriod(accountId, billingPeriod);
            if (reading == null) return NotFound();
            return Ok(reading);
        }

        [HttpGet("GetByReader/{readerId}")]
        public async Task<IActionResult> GetByReader(int readerId)
        {
            var readings = await _service.GetByReader(readerId);
            if (readings == null) return NotFound();
            return Ok(readings);
        }

        [HttpGet("GetByZoneAndBook/{zone}/{book}")]
        public async Task<IActionResult> GetByZoneAndBook(int zone, int book)
        {
            var readings = await _service.GetByZoneAndBook(zone, book);
            if (readings == null || !readings.Any()) return NotFound();
            return Ok(readings);
        }

        [HttpPost("SaveUpdate/{userId}/{reading}")]
        public async Task<IActionResult> SaveUpdate(int userId, Reading reading)
        {
            var savedReading = await _service.SaveUpdate(userId, reading);
            return Ok(savedReading);
        }

        [HttpGet("GetAccountPreviousReading/{accountId}")]
        public async Task<IActionResult> GetAccountPreviousReading(int accountId)
        {
            var reading = await _service.GetAccountPreviousReading(accountId);
            if(reading == null) return NotFound();
            return Ok(reading);
        }

        [HttpGet("GetAccountCurrentReading/{accountId}")]
        public async Task<IActionResult> GetAccountCurrentReading(int accountId)
        {
            var reading = await _service.GetAccountCurrentReading(accountId);
            if (reading == null) return NotFound();
            return Ok(reading);
        }

        #endregion

    }
}