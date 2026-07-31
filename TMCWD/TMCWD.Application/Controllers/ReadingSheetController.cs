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
        public async Task<IActionResult> CreateReadingSheet(int zone, int book, [FromBody] ReadingSheet readingSheet)
        {

            ReadingSheet savedSheet = new(); 

            try
            {

                var assignedToUser = await _userTrans.Get(readingSheet.AssignedTo);

                string name = $"{readingSheet.BillingDate.ToString("MM-dd-yyyy")} {assignedToUser.Name}";
                readingSheet.Name = name ;

                var zoneBook = await _zoneBookTrans.GetByZoneAndBook(zone, book);

                if (zoneBook == null) return BadRequest();

                readingSheet.DateCreated = DateTime.Now;
                readingSheet.CreatedBy = _user.User.Id;
                readingSheet.ZoneBookId = zoneBook.Id;
                readingSheet.Status = ReadingStatus.InProgress;

                savedSheet = await _readingSheetTrans.SaveUpdate(_user.User.Id, readingSheet);

                if(savedSheet != null)
                {
                    var accounts = await _accountTransaction.GetByZoneBookAndSequence(zone, book, readingSheet.SequenceFrom, readingSheet.SequenceTo);
                    var readings = from accts in accounts
                                   select new Reading
                                   {
                                       AccountId = accts.Id,
                                       CreatedBy = _user.User.Id,
                                       CurrentReading = 0,
                                       DateCreated = DateTime.Now,
                                       DateUpdated = DateTime.Now,
                                       Status = ReadingStatus.InProgress,
                                       ReadingSheetId = savedSheet.Id,
                                       UpdatedBy = _user.User.Id
                                   };
                    var savedReadings = await _readingTransaction.SaveMultiple([..readings]);
                }

                return Ok(savedSheet);

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

        [HttpGet]
        public async Task<IActionResult> GetAllReadingSheets()
        {
            var readingSheets = await _readingSheetTrans.GetAll();
            if(readingSheets == null) return NotFound();
            var userIds = readingSheets.Select(x => x.AssignedTo).ToList();
            var readingSheetIds = readingSheets.Select(x => x.Id).ToList();
            var zoneBookIds = readingSheets.Select(x => x.ZoneBookId).ToList();
            Task<List<User>> getUsersTask = _userTrans.GetUsersById(userIds);
            Task<List<Reading>> getReadingsTask = _readingTransaction.GetRangeByReadingSheetIds(readingSheetIds);
            Task<List<ZoneBook>> getZoneBooksTask = _zoneBookTrans.GetByIds(zoneBookIds);

            await Task.WhenAll(getUsersTask, getReadingsTask, getZoneBooksTask);
            var users = getUsersTask.Result;
            var readings = getReadingsTask.Result;
            var zoneBooks = getZoneBooksTask.Result;

            var readingSheetData = from rs in readingSheets
                                   join usrs in users on rs.AssignedTo equals usrs.Id
                                   join zb in zoneBooks on rs.ZoneBookId equals zb.Id
                                   select new
                                   {
                                       Id = rs.Id,
                                       MeterReader = usrs.Name,
                                       BillingDate = rs.BillingDate,
                                       Zone = $"ZN-{zb.Zone.ToString().PadLeft(2, '0')}",
                                       ForPosting = GetTotalCompletedReadings(readings, rs.Id),
                                       TotalInProgress = GetTotalInProgressReadings(readings, rs.Id),
                                       TotalAccounts = GetTotalReadings(readings, rs.Id),
                                       TotalCompleted = GetTotalCompletedReadings(readings, rs.Id),
                                       Status = rs.Status.Description()
                                   };


            if (readingSheetData == null) return NotFound();
            return Ok(readingSheetData);
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteReadingSheet(int id)
        {
            var readingSheet = await _readingSheetTrans.Get(id);
            List<int> listIds = new();
            listIds.Add(readingSheet.Id);
            if (readingSheet == null) return NotFound();
            readingSheet.Status = ReadingStatus.Deleted;
            readingSheet.UpdatedBy = _user.User.Id;
            readingSheet.DateUpdated = DateTime.Now;

            Task<ReadingSheet> updateReadingSheetTask = _readingSheetTrans.SaveUpdate(_user.User.Id, readingSheet);
            Task<List<Reading>> updateStatusByReadingSheetIdTask = _readingTransaction.UpdateStatusByReadingSheetIds(listIds, ReadingStatus.Deleted, _user.User.Id);

            await Task.WhenAll(updateStatusByReadingSheetIdTask, updateStatusByReadingSheetIdTask);

            var updatedReadingSheet = updateReadingSheetTask.Result;
            var updatedReadings = updateStatusByReadingSheetIdTask.Result;

            if (updatedReadingSheet == null || updatedReadings == null) return Ok("Problems occurred while deleting reading sheet");

            return Ok(true);
        }

        [HttpDelete]
        public async Task<IActionResult> BulkDeleteReadingSheet(int[] ids, int status)
        {

            Task<List<ReadingSheet>> updateReadingSheetTask = _readingSheetTrans.UpdateReadingSheetsStatus(ids.ToList(), _user.User.Id, (ReadingStatus)status);
            Task<List<Reading>> updateReadingTask = _readingTransaction.UpdateStatusByReadingSheetIds(ids.ToList(), (ReadingStatus)status, _user.User.Id);

            await Task.WhenAll(updateReadingSheetTask, updateReadingTask);

            var readingSheets = updateReadingSheetTask.Result;
            var readings = updateReadingTask.Result;

            if (readingSheets == null || readings == null) return Ok("Problems encountered while processing bulk delete");

            return Ok(true);
        }

        private int GetTotalInProgressReadings(List<Reading> readings, int readingSheetId)
        {
            return readings.Where(x => x.Status == ReadingStatus.InProgress && x.ReadingSheetId == readingSheetId).Count();
        }

        private int GetTotalCompletedReadings(List<Reading> readings, int readingSheetId)
        {
            return readings.Where(x => x.Status == ReadingStatus.Completed && x.ReadingSheetId == readingSheetId).Count();
        }

        private int GetTotalReadings(List<Reading> readings, int readingSheetId)
        {
            return readings.Where(x => x.ReadingSheetId == readingSheetId).Count();
        }

        #endregion

    }
}
