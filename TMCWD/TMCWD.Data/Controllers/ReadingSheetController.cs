using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;
using TMCWD.Model.Billing.Responses;
namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReadingSheetController : Controller
    {
        #region fields

        private readonly IReadingSheetService _readingSheetService;

        #endregion

        #region constructor

        public ReadingSheetController(IReadingSheetService readingSheetService)
        {
            _readingSheetService = readingSheetService;
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var readingSheet = await _readingSheetService.Get(id);
            if(readingSheet == null) return NotFound();
            return Ok(readingSheet);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var readingSheets = await _readingSheetService.GetAll();
            if (readingSheets == null || !readingSheets.Any()) return NotFound();
            return Ok(readingSheets);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetByAssignedTo/{assignedTo}")]
        public async Task<IActionResult> GetByAssignedTo(int assignedTo)
        {
            var readingSheets = await _readingSheetService.GetByAssignedTo(assignedTo);
            if (readingSheets == null || !readingSheets.Any()) return NotFound();
            return Ok(readingSheets);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetByZoneAndBook/{zone}/{book}")]
        public async Task<IActionResult> GetByZoneAndBook(int zone, int book)
        {
            var readingSheets = await _readingSheetService.GetByZoneAndBook(zone, book);
            if (readingSheets == null || !readingSheets.Any()) return NotFound();
            return Ok(readingSheets);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetByZoneBookAndAssignedTo/{zone}/{book}/{assignedTo}")]
        public async Task<IActionResult> GetByZoneBookAndAssignedTo(int zone, int book, int assignedTo)
        {
            var readingSheets = await _readingSheetService.GetByZoneBookAndAssignedTo(zone, book, assignedTo);
            if (readingSheets == null || !readingSheets.Any()) return NotFound();
            return Ok(readingSheets);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetByBillingDate/{zone}/{book}/{billingDate}")]
        public async Task<IActionResult> GetByBillingDate(int zone, int book, DateTime billingDate)
        {
            var readingSheet = await _readingSheetService.GetByBillingDate(zone, book, billingDate);
            if (readingSheet == null) return NotFound();
            return Ok(readingSheet);
        }

        [HttpPost("SaveUpdate/{userId}")]
        [Authorize(Policy = "BillingOperations")]
        public async Task<IActionResult> SaveUpdate(int userId, [FromBody] ReadingSheet readingSheet)
        {
            userId = ActorId;
            if (userId <= 0) return Unauthorized();
            var savedReadingSheet = await _readingSheetService.SaveUpdate(userId, readingSheet);
            return Ok(savedReadingSheet);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetCurrentByAssignedTo/{zone}/{book}/{assignedTo}")]
        public async Task<IActionResult> GetCurrentByAssignedTo(int zone, int book, int assignedTo)
        {
            var readingSheet = await _readingSheetService.GetCurrentByAssignedTo(zone, book, assignedTo);
            if(readingSheet == null) return NotFound();
            return Ok(readingSheet);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet]
        [Route("GetReadingSheetAccounts")]
        public async Task<IActionResult> GetReadingSheetAccounts(int readingSheetId)
        {
            var accounts = await _readingSheetService.GetAccountsForReadingSheet(readingSheetId);
            return Ok(accounts);
        }

        [HttpPost("UpdateZoneProgress")]
        [Authorize(Policy = "BillingOperations")]
        public async Task<IActionResult> UpdateZoneProgress([FromBody] UpdateZoneProgressRequest request)
        {
            if (ActorId <= 0) return Unauthorized();
            if (request == null || request.ReadingSheetId <= 0)
            {
                return BadRequest(new { message = "Invalid request data" });
            }

            var result = await _readingSheetService.UpdateZoneProgress(
                request.ReadingSheetId, 
                request.CompletedCount, 
                request.TotalCount);

            if (!result)
            {
                return NotFound(new { message = "Reading sheet not found or no readings exist" });
            }

            return Ok(new { message = "Zone progress updated successfully" });
        }

        [HttpPatch("UpdateReadingSheetsStatus/{userId}/{status}")]
        [Authorize(Policy = "BillingOperations")]
        public async Task<IActionResult> UpdateReadingSheetsStatus(int userId, int status, [FromQuery] long[] ids)
        {
            userId = ActorId;
            if (userId <= 0) return Unauthorized();
            if (ids == null || !ids.Any())
            {
                return BadRequest(new { message = "Sheet IDs are required" });
            }

            var updatedSheets = await _readingSheetService.UpdateReadingSheetsStatus(
                ids,
                userId,
                status
            );

            return Ok(updatedSheets);
        }

        [HttpPost]
        [Route("PartialPostReadingSheet")]
        [Authorize(Policy = "BillingOperations")]
        public async Task<IActionResult> PartialPostReadingSheet(int readingSheetId)
        {
            var userId = ActorId;
            if (userId <= 0) return Unauthorized();
            try
            {
                var postedCount = await _readingSheetService.PartialPost(readingSheetId, userId);
                return Ok(new { postedCount });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpPost]
        [Route("CompleteReadingSheet")]
        [Authorize(Policy = "BillingOperations")]
        public async Task<IActionResult> CompleteReadingSheet(int readingSheetId)
        {
            var userId = ActorId;
            if (userId <= 0) return Unauthorized();
            var sheet = await _readingSheetService.CompleteReadingSheet(readingSheetId, userId);

            if (sheet == null)
                return BadRequest("Cannot complete � some accounts are still pending, or the reading sheet was not found.");

            return Ok(sheet);
        }
        #endregion
        private int ActorId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
    }

    // Request model for UpdateZoneProgress
    public class UpdateZoneProgressRequest
    {
        public int ReadingSheetId { get; set; }
        public int CompletedCount { get; set; }
        public int TotalCount { get; set; }
    }
}
