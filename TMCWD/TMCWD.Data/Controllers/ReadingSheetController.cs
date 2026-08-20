using Microsoft.AspNetCore.Mvc;
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

        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var readingSheet = await _readingSheetService.Get(id);
            if(readingSheet == null) return NotFound();
            return Ok(readingSheet);
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var readingSheets = await _readingSheetService.GetAll();
            if (readingSheets == null || !readingSheets.Any()) return NotFound();
            return Ok(readingSheets);
        }

        [HttpGet("GetByAssignedTo/{assignedTo}")]
        public async Task<IActionResult> GetByAssignedTo(int assignedTo)
        {
            var readingSheets = await _readingSheetService.GetByAssignedTo(assignedTo);
            if (readingSheets == null || !readingSheets.Any()) return NotFound();
            return Ok(readingSheets);
        }

        [HttpGet("GetByZoneAndBook/{zone}/{book}")]
        public async Task<IActionResult> GetByZoneAndBook(int zone, int book)
        {
            var readingSheets = await _readingSheetService.GetByZoneAndBook(zone, book);
            if (readingSheets == null || !readingSheets.Any()) return NotFound();
            return Ok(readingSheets);
        }

        [HttpGet("GetByZoneBookAndAssignedTo/{zone}/{book}/{assignedTo}")]
        public async Task<IActionResult> GetByZoneBookAndAssignedTo(int zone, int book, int assignedTo)
        {
            var readingSheets = await _readingSheetService.GetByZoneBookAndAssignedTo(zone, book, assignedTo);
            if (readingSheets == null || !readingSheets.Any()) return NotFound();
            return Ok(readingSheets);
        }

        [HttpGet("GetByBillingDate/{zone}/{book}/{billingDate}")]
        public async Task<IActionResult> GetByBillingDate(int zone, int book, DateTime billingDate)
        {
            var readingSheet = await _readingSheetService.GetByBillingDate(zone, book, billingDate);
            if (readingSheet == null) return NotFound();
            return Ok(readingSheet);
        }

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<IActionResult> SaveUpdate(int userId, [FromBody] ReadingSheet readingSheet)
        {
            var savedReadingSheet = await _readingSheetService.SaveUpdate(userId, readingSheet);
            return Ok(savedReadingSheet);
        }

        [HttpGet("GetCurrentByAssignedTo/{zone}/{book}/{assignedTo}")]
        public async Task<IActionResult> GetCurrentByAssignedTo(int zone, int book, int assignedTo)
        {
            var readingSheet = await _readingSheetService.GetCurrentByAssignedTo(zone, book, assignedTo);
            if(readingSheet == null) return NotFound();
            return Ok(readingSheet);
        }

        [HttpGet]
        [Route("GetReadingSheetAccounts")]
        public async Task<IActionResult> GetReadingSheetAccounts(int readingSheetId)
        {
            var accounts = await _readingSheetService.GetAccountsForReadingSheet(readingSheetId);
            return Ok(accounts);
        }

        [HttpPost("UpdateZoneProgress")]
        public async Task<IActionResult> UpdateZoneProgress([FromBody] UpdateZoneProgressRequest request)
        {
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
        public async Task<IActionResult> UpdateReadingSheetsStatus(int userId, int status, [FromQuery] long[] ids)
        {
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
        public async Task<IActionResult> PartialPostReadingSheet(int readingSheetId, int userId)
        {
            var postedCount = await _readingSheetService.PartialPost(readingSheetId, userId);
            return Ok(new { postedCount });
        }

        [HttpPost]
        [Route("CompleteReadingSheet")]
        public async Task<IActionResult> CompleteReadingSheet(int readingSheetId, int userId)
        {
            var sheet = await _readingSheetService.CompleteReadingSheet(readingSheetId, userId);

            if (sheet == null)
                return BadRequest("Cannot complete — some accounts are still pending, or the reading sheet was not found.");

            return Ok(sheet);
        }
        #endregion
    }

    // Request model for UpdateZoneProgress
    public class UpdateZoneProgressRequest
    {
        public int ReadingSheetId { get; set; }
        public int CompletedCount { get; set; }
        public int TotalCount { get; set; }
    }
}