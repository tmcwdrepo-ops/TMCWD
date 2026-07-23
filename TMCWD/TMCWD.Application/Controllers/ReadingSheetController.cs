using Microsoft.AspNetCore.Mvc;
using TMCWD.Administration;
using TMCWD.Billing;
using TMCWD.CustomerSupport;
using TMCWD.Model.Administrator;
using TMCWD.Model.Billing;
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
        private readonly AccountTransaction _accountTransaction;
        private readonly ReadingTransaction _readingTransaction;

        #endregion

        #region constructors

        public ReadingSheetController(AuthenticatedUserService user,
            ReadingSheetTransaction readingSheetTrans,
            UserTransaction userTrans,
            ZoneBookTransaction zoneBookTrans,
            AccountTransaction accountTransaction,
            ReadingTransaction readingTransaction)
        {
            _user = user;
            _readingSheetTrans = readingSheetTrans;
            _userTrans = userTrans;
            _zoneBookTrans = zoneBookTrans;
            _accountTransaction = accountTransaction;
            _readingTransaction = readingTransaction;
        }

        #endregion

        #region methods

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
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

                if(savedSheet != null)
                {
                    var accounts = await _accountTransaction.GetByZoneBookId(savedSheet.ZoneBookId);
                    var readings = from accts in accounts
                                   select new Reading
                                   {
                                       AccountId = accts.Id,
                                       CreatedBy = _user.User.Id,
                                       CurrentReading = 0,
                                       DateCreated = DateTime.Now,
                                       DateUpdated = DateTime.Now,
                                       IsCompleted = false,
                                       ReadingSheetId = savedSheet.Id,
                                       UpdatedBy = _user.User.Id,
                                   };
                    var savedReadings = await _readingTransaction.SaveMultiple([..readings]);
                }

            }
            catch { }

            return Ok(savedSheet);
        }

        [HttpGet]
        public async Task<IActionResult> GetCurrentByAssignedTo(int zone, int book, int assignedTo)
        {
            var sheet = await _readingSheetTrans.GetCurrentByAssignedTo(zone, book, assignedTo);
            return Ok(sheet);
        }

        [HttpGet]
        public async Task<IActionResult> GetByAssignedTo(int assignedTo)
        {
            var readingSheets = await _readingSheetTrans.GetByAssignedTo(assignedTo);
            return Ok(readingSheets);
        }

        [HttpGet]
        public async Task<IActionResult> GetZones()
        {

            List<ZoneBook> zones = new();
            try
            {
                zones = await _zoneBookTrans.GetAll();
                if (zones == null) return BadRequest();

            }
            catch { }

            return Ok(zones);
        }

        public async Task<IActionResult> GetBooksByZone(int zone)
        {
            var books = await _zoneBookTrans.GetBooksByZone(zone);
            if(books == null) return NotFound();
            return Ok(books);
        }

        #endregion

    }
}
