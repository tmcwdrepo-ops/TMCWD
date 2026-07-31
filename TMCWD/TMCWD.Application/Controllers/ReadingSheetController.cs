using Microsoft.AspNetCore.Mvc;
using System.Linq;
using TMCWD.Administration;
using TMCWD.Billing;
using TMCWD.CustomerSupport;
using TMCWD.Model.Administrator;
using TMCWD.Model.Billing;
using TMCWD.Model.CustomerSupport;
using TMCWD.Services;

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

        [HttpGet]
        public async Task<IActionResult> CreateReadingSheet(int zone, int book, int assignedTo, DateTime billingPeriod)
        {

            ReadingSheet savedSheet = new();

            try
            {

                var assignedToUser = await _userTrans.Get(assignedTo);

                string name = $"{DateTime.Now.ToString("MM - dd - yyyy")} - {assignedToUser.Name}";

                var zoneBook = await _zoneBookTrans.GetByZoneAndBook(zone, book);

                if (zoneBook == null) return BadRequest();

                ReadingSheet sheet = new ReadingSheet
                {
                    AssignedTo = assignedTo,
                    BillingDate = billingPeriod,
                    Name = name,
                    CreatedBy = _user.User.Id,
                    ZoneBookId = zoneBook.Id,
                    DateCreated = DateTime.Now
                };

                savedSheet = await _readingSheetTrans.SaveUpdate(_user.User.Id, sheet);
            }
            catch { }

            return Ok(savedSheet);
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
            if (readingSheets == null || !readingSheets.Any()) return NotFound();
            return Ok(readingSheets);
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

    public class SaveTemplateRequest
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int ReaderId { get; set; }
        public int Zone { get; set; }
        public int Book { get; set; }
        public bool IsActive { get; set; } = true;
    }
}