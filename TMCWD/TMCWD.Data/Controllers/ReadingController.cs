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

        [HttpGet("GetByZoneAndBook/{zone}/{book}")]
        public async Task<IActionResult> GetByZoneAndBook(int zone, int book)
        {
            var readings = await _service.GetByZoneAndBook(zone, book);
            if (readings == null || !readings.Any()) return NotFound();
            return Ok(readings);
        }

        [HttpGet("GetByAccount/{accountId}")]
        public async Task<IActionResult> GetByAccount(int accountId)
        {
            var readings = await _service.GetByAccount(accountId);
            if (readings == null || !readings.Any()) return NotFound();
            return Ok(readings);
        }

        [HttpGet("GetCurrentByAccountZoneBook/{zone}/{book}/{accountId}")]
        public async Task<IActionResult> GetCurrentByAccountZoneBook(int zone, int book, int accountId)
        {
            var reading = await _service.GetCurrentByAccountZoneBook(zone, book, accountId);
            if (reading == null) return NotFound();
            return Ok(reading);
        }

        [HttpGet("GetByReadingSheetId/{readingSheetId}")]
        public async Task<IActionResult> GetByReadingSheetId(int readingSheetId)
        {
            var readings = await _service.GetByReadingSheetId(readingSheetId);
            if (readings == null || !readings.Any()) return NotFound();
            return Ok(readings);
        }

        [HttpGet("GetAccountPreviousReading/{accountId}")]
        public async Task<IActionResult> GetAccountPreviousReading(int accountId)
        {
            var reading = await _service.GetAccountPreviousReading(accountId);
            if (reading == null) return NotFound();
            return Ok(reading);
        }

        [HttpGet("GetAccountCurrentReading/{accountId}")]
        public async Task<IActionResult> GetAccountCurrentReading(int accountId)
        {
            var reading = await _service.GetAccountCurrentReading(accountId);
            if(reading == null) return NotFound();
            return Ok(reading);
        }

        [HttpGet("GetByReader/{readerId}")]
        public async Task<IActionResult> GetByReader(int readerId)
        {
            var readings = await _service.GetByReader(readerId);
            if (readings == null || !readings.Any()) return NotFound();
            return Ok(readings);
        }

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<IActionResult> SaveUpdate(int userId, Reading reading)
        {
            var savedReading = await _service.SaveUpdate(userId, reading);
            return Ok(savedReading);
        }

        [HttpPost("SaveMultiple")]
        public async Task<IActionResult> SaveMultiple(List<Reading> readings)
        {
            var savedReadings = await _service.SaveMultiple(readings);
            return Ok(savedReadings);
        }

        [HttpGet("GetReadingByBillingPeriod/{zone}/{book}/{billingPeriod}")]
        public async Task<IActionResult> GetReadingByBillingPeriod(int zone, int book, DateTime billingPeriod)
        {
            var readings = await _service.GetReadingsByBillingPeriod(zone, book, billingPeriod);
            if (readings == null || !readings.Any()) return NotFound();
            return Ok(readings);
        }

        #endregion

    }
}