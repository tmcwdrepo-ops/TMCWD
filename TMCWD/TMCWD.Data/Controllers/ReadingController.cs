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

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var reading = await _service.Get(id);
            if (reading == null) return NotFound();
            return Ok(reading);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetByAccount/{accountId}")]
        public async Task<IActionResult> GetByAccount(int accountId)
        {
            var readings = await _service.GetByAccount(accountId);
            if (readings == null || !readings.Any()) return NotFound();
            return Ok(readings);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetByAccountAndBillingPeriod/{accountId}/{billingPeriod}")]
        public async Task<IActionResult> GetByAccountAndBillingPeriod(int accountId, DateTime billingPeriod)
        {
            var reading = await _service.GetByAccountAndBillingPeriod(accountId, billingPeriod);
            if (reading == null) return NotFound();
            return Ok(reading);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetByReader/{readerId}")]
        public async Task<IActionResult> GetByReader(int readerId)
        {
            var readings = await _service.GetByReader(readerId);
            if (readings == null) return NotFound();
            return Ok(readings);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetByZoneAndBook/{zone}/{book}")]
        public async Task<IActionResult> GetByZoneAndBook(int zone, int book)
        {
            var readings = await _service.GetByZoneAndBook(zone, book);
            if (readings == null || !readings.Any()) return NotFound();
            return Ok(readings);
        }

        [HttpPost("SaveUpdate/{userId}")]
        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        public async Task<IActionResult> SaveUpdate(int userId, [FromBody] Reading reading)
        {
            if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out userId) || userId <= 0)
                return Unauthorized();
            try
            {
                if (reading.Id > 0)
                {
                    var existing = await _service.Get(checked((int)reading.Id));
                    if (existing == null) return NotFound();
                    if (existing.AccountId != reading.AccountId || existing.ReadingSheetId != reading.ReadingSheetId)
                        return Conflict("A reading cannot be moved to another account or sheet.");
                }
                var savedReading = await _service.Sync(userId, checked((int)reading.ReadingSheetId),
                    new TMCWD.Model.Billing.Requests.MobileReadingUpload
                    {
                        AccountId = checked((int)reading.AccountId), CurrentReading = reading.CurrentReading,
                        ReadingDate = reading.DateCreated, Remarks = reading.Remarks ?? string.Empty
                    }, requireAssignment: false);
                return Ok(savedReading);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch (KeyNotFoundException) { return NotFound(); }
            catch (ArgumentException ex) { return BadRequest(ex.Message); }
            catch (OverflowException) { return BadRequest("Invalid account or reading sheet identity."); }
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetAccountPreviousReading/{accountId}")]
        public async Task<IActionResult> GetAccountPreviousReading(int accountId)
        {
            var reading = await _service.GetAccountPreviousReading(accountId);
            if(reading == null) return NotFound();
            return Ok(reading);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetAccountCurrentReading/{accountId}")]
        public async Task<IActionResult> GetAccountCurrentReading(int accountId)
        {
            var reading = await _service.GetAccountCurrentReading(accountId);
            if (reading == null) return NotFound();
            return Ok(reading);
        }

        [HttpPost("SaveRange")]
        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "PendingAuthorizationDecision")]
        public async Task<IActionResult> SaveRange([FromBody] List<Reading> readings)
        {
            var savedReadings = await _service.SaveRange(readings);
            if(savedReadings == null || !savedReadings.Any()) return NotFound();
            return Ok(savedReadings);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpPost("GetRangeByReadingSheetIds")]
        public async Task<IActionResult> GetRangeByReadingSheetIds([FromBody] List<long> readingSheetIds)
        {
            var readings = await _service.GetByReadingSheetIds(readingSheetIds);
            return Ok(readings ?? new List<Reading>());
        }

        #endregion

    }
}
