using Microsoft.AspNetCore.Mvc;
using TMCWD.Administration;
using TMCWD.Billing;
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

        #endregion

        #region constructors

        public ReadingSheetController(AuthenticatedUserService user,
            ReadingSheetTransaction readingSheetTrans,
            UserTransaction userTrans,
            ZoneBookTransaction zoneBookTrans)
        {
            _user = user;
            _readingSheetTrans = readingSheetTrans;
            _userTrans = userTrans;
            _zoneBookTrans = zoneBookTrans;
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
