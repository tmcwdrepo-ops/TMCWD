using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography.X509Certificates;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

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

    }
}
    #endregion