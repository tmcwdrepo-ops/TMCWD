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
        private readonly AccountTransaction _accountTransaction;
        private readonly ReadingTransaction _readingTransaction;
        private readonly ReadingSheetTemplateTransaction _readingSheetTemplateTrans;

        #endregion

        #region constructors

        public ReadingSheetController(AuthenticatedUserService user,
            ReadingSheetTransaction readingSheetTrans,
            UserTransaction userTrans,
            ZoneBookTransaction zoneBookTrans,
            AccountTransaction accountTransaction,
            ReadingTransaction readingTransaction,
            ReadingSheetTemplateTransaction readingSheetTemplateTrans)
        {
            _user = user;
            _readingSheetTrans = readingSheetTrans;
            _userTrans = userTrans;
            _zoneBookTrans = zoneBookTrans;
            _accountTransaction = accountTransaction;
            _readingTransaction = readingTransaction;
            _readingSheetTemplateTrans = readingSheetTemplateTrans;
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