using Microsoft.AspNetCore.Mvc;
using System.Linq;
using TMCWD.Administration;
using TMCWD.Billing;
using TMCWD.CustomerSupport;
using TMCWD.Model.Administrator;
using TMCWD.Model.Billing;
using TMCWD.Model.CustomerSupport;
using TMCWD.Services;
using TMCWD.Model.Billing.Requests;

namespace TMCWD.Application.Controllers
{
    public class ReadingSheetController : Controller
    {
        #region fields

        private readonly AuthenticatedUserService _user;
        private readonly ReadingSheetTransaction _readingSheetTrans;
        private readonly ZoneBookTransaction _zoneBookTrans;
        private readonly UserTransaction _userTrans;
        private readonly ReadingTransaction _readingTransaction;
        private readonly ReadingSheetTemplateTransaction _readingSheetTemplateTrans;
        private readonly AccountTransaction _accountTrans;
        #endregion

        #region constructors

        public ReadingSheetController(AuthenticatedUserService user,
            ReadingSheetTransaction readingSheetTrans,
            UserTransaction userTrans,
            ZoneBookTransaction zoneBookTrans,
            ReadingTransaction readingTransaction,
            ReadingSheetTemplateTransaction readingSheetTemplateTrans,
            AccountTransaction accountTrans)
        {
            _user = user;
            _readingSheetTrans = readingSheetTrans;
            _userTrans = userTrans;
            _zoneBookTrans = zoneBookTrans;
            _readingTransaction = readingTransaction;
            _readingSheetTemplateTrans = readingSheetTemplateTrans;
            _accountTrans = accountTrans;
        }

        #endregion

        #region methods

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateReadingSheet([FromBody] SaveReadingSheetRequest request)
        {
            var assignedToUser = await _userTrans.Get(request.AssignedTo);
            if (assignedToUser == null) return BadRequest("Selected meter reader was not found.");

            var zoneBook = await _zoneBookTrans.GetByZoneAndBook(request.Zone, request.Book);
            if (zoneBook == null) return BadRequest("Zone/Book combination not found.");

            var sheet = new ReadingSheet
            {
                Name = $"{DateTime.Now:MM - dd - yyyy} - {assignedToUser.Name}",
                AssignedTo = request.AssignedTo,
                BillingDate = request.BillingPeriod,
                DueDate = request.DueDate,
                DisconnectionDate = request.DisconnectionDate,
                BillingPeriodStart = request.BillingPeriodStart,
                SeqFrom = request.SeqFrom,
                SeqTo = request.SeqTo,
                ZoneBookId = zoneBook.Id
            };

            var savedSheet = await _readingSheetTrans.SaveUpdate(_user.User.Id, sheet);

            if (savedSheet == null || savedSheet.Id <= 0)
                return BadRequest("Failed to save reading sheet.");

            return Ok(savedSheet);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateReadingSheet(
            [FromBody] ReadingSheet request)
        {
            if (request == null || request.Id <= 0)
                return BadRequest("Invalid reading sheet.");

            var existing = await _readingSheetTrans.Get((int)request.Id);

            if (existing == null)
                return NotFound("Reading sheet not found.");

            // Update the editable fields
            existing.Name = request.Name;
            existing.BillingDate = request.BillingDate;
            existing.DueDate = request.DueDate;
            existing.DisconnectionDate = request.DisconnectionDate;
            existing.BillingPeriodStart = request.BillingPeriodStart;
            existing.AssignedTo = request.AssignedTo;
            existing.ZoneBookId = request.ZoneBookId;
            existing.SeqFrom = request.SeqFrom;
            existing.SeqTo = request.SeqTo;

            // IMPORTANT:
            // This is what persists In-Progress / Completed.
            existing.Status = request.Status;

            var saved = await _readingSheetTrans.SaveUpdate(
                _user.User.Id,
                existing
            );

            if (saved == null)
                return BadRequest("Failed to update reading sheet.");

            return Ok(saved);
        }

        [HttpGet]
        public async Task<IActionResult> GetCurrentByAssignedTo(int assignedTo)
        {
            var sheet = await _readingSheetTrans.GetCurrentByAssignedTo(assignedTo);
            return Ok(sheet);
        }

        [HttpGet]
        public async Task<IActionResult> GetByAssignedTo(int assignedTo)
        {
            var readingSheets = await _readingSheetTrans.GetByAssignedTo(assignedTo);
            return Ok(readingSheets);
        }

        
        [HttpGet]
        public async Task<IActionResult> GetAllReadingSheet()
        {
            var readingSheets = await _readingSheetTrans.GetAll();

            if (readingSheets == null || !readingSheets.Any())
                return Ok(new List<object>());

            var result = new List<object>();

            foreach (var sheet in readingSheets)
            {
                // Get the meter reader's name
                var reader = await _userTrans.Get((int)sheet.AssignedTo);

                // Get the Zone and Book using ZoneBookId
                var zoneBook = await _zoneBookTrans.Get(sheet.ZoneBookId.ToString());

                result.Add(new
                {
                    id = sheet.Id,
                    name = sheet.Name,

                    meterReader = reader?.Name ?? "—",

                    billingDate = sheet.BillingDate,
                    dueDate = sheet.DueDate,
                    disconnectionDate = sheet.DisconnectionDate,
                    billingPeriodStart = sheet.BillingPeriodStart,

                    zone = zoneBook?.Zone ?? 0,
                    book = zoneBook?.Book ?? 0,

                    seqFrom = sheet.SeqFrom,
                    seqTo = sheet.SeqTo,

                    assignedTo = sheet.AssignedTo,
                    zoneBookId = sheet.ZoneBookId,

                    status = sheet.Status,

                    createdBy = sheet.CreatedBy,
                    dateCreated = sheet.DateCreated,
                    dateUpload = sheet.DateUpload
                });
            }

            return Ok(result);
        }



        [HttpGet]
        public async Task<IActionResult> GetReadingSheetTemplates()
        {
            var templates = (await _readingSheetTemplateTrans.GetAll() ?? new List<ReadingSheetTemplate>())
                .Where(t => t.IsActive)
                .ToList();
            var result = new List<object>();
            foreach (var t in templates)
            {
                var reader = await _userTrans.Get(t.ReaderId);
                var zoneBook = await _zoneBookTrans.Get(t.ZoneBookId.ToString());

                result.Add(new
                {
                    id = t.Id,
                    name = t.Name,
                    readerId = t.ReaderId,
                    readerName = reader?.Name ?? "—",
                    zone = zoneBook?.Zone ?? 0,
                    book = zoneBook?.Book ?? 0
                });
            }

            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetReaders()
        {
            var readers = await _userTrans.GetUsersByRole(UserRole.MeterReader) ?? new List<User>();
            return Ok(readers.Select(r => new { id = r.Id, name = r.Name }));
        }

        [HttpGet]
        public async Task<IActionResult> GetZones()
        {
            var zoneBooks = await _zoneBookTrans.GetAll() ?? new List<ZoneBook>();
            var result = zoneBooks
                .Select(z => new { value = z.Zone, label = "Zone " + z.Zone })
                .DistinctBy(z => z.value)
                .OrderBy(z => z.value);
            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetBooksByZone(int zone)
        {
            var zoneBooks = await _zoneBookTrans.GetBooksByZone(zone) ?? new List<ZoneBook>();
            return Ok(zoneBooks.Select(b => new { value = b.Book, label = "Book " + b.Book }));
        }

        [HttpPost]
        public async Task<IActionResult> DeactivateReadingSheetTemplate(int id)
        {
            var existing = await _readingSheetTemplateTrans.Get(id);
            if (existing == null) return NotFound();

            existing.IsActive = false;

            var saved = await _readingSheetTemplateTrans.SaveUpdate(_user.User.Id, existing);
            return Ok(saved);
        }

        [HttpPost]
        public async Task<IActionResult> SaveReadingSheetTemplate([FromBody] SaveTemplateRequest request)
        {
            var zoneBook = await _zoneBookTrans.GetByZoneAndBook(request.Zone, request.Book);
            if (zoneBook == null) return BadRequest("Zone/Book combination not found.");

            var template = new ReadingSheetTemplate
            {
                Id = request.Id,
                Name = request.Name,
                ReaderId = request.ReaderId,
                ZoneBookId = zoneBook.Id,
                CreatedBy = _user.User.Id
            };

            var saved = await _readingSheetTemplateTrans.SaveUpdate(_user.User.Id, template);
            return Ok(saved);
        }

        [HttpGet]
        public async Task<IActionResult> GetAccountsByZoneAndBook(int zone, int book)
        {
            // NOTE: "Unbilled" scope isn't filterable yet — Account has no billed-status
            // field. All accounts for this zone/book are returned regardless of scope;
            // Ranged (sequence) filtering happens client-side in JS.
            var accounts = await _accountTrans.GetByZoneAndBook(zone, book) ?? new List<Account>();

            var result = accounts.Select(a => new
            {
                accountNumber = a.AccountNumber,
                address = a.FullAddress,
                classification = a.Classification.ToString(),
                sequence = a.Sequence
            });

            return Ok(result);
        }


        
        
        #endregion

    }

    
}