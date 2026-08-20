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
        private readonly WaterRateTransaction _waterRateTrans;

        #endregion

        #region constructors

        public ReadingSheetController(AuthenticatedUserService user,
            ReadingSheetTransaction readingSheetTrans,
            UserTransaction userTrans,
            ZoneBookTransaction zoneBookTrans,
            AccountTransaction accountTransaction,
            ReadingTransaction readingTransaction,
            ReadingSheetTemplateTransaction readingSheetTemplateTrans,
            WaterRateTransaction waterRateTrans)
{
    _user = user;
    _readingSheetTrans = readingSheetTrans;
    _userTrans = userTrans;
    _zoneBookTrans = zoneBookTrans;
    _accountTransaction = accountTransaction;
    _readingTransaction = readingTransaction;
    _readingSheetTemplateTrans = readingSheetTemplateTrans;
    _waterRateTrans = waterRateTrans;
}

        #endregion

        #region methods

        public IActionResult Index()
        {
            return View();
        }

      [HttpPost]
        public async Task<IActionResult> CreateReadingSheet(
             int zone,
             int book,
             [FromBody] ReadingSheet readingSheet)
        {
            var assignedToUser = await _userTrans.Get(readingSheet.AssignedTo);

            if (assignedToUser == null)
                return BadRequest("Selected meter reader was not found.");

            var zoneBook = await _zoneBookTrans.GetByZoneAndBook(zone, book);

            if (zoneBook == null)
                return BadRequest("Zone/Book combination not found.");

            var sheet = new ReadingSheet
            {
                Name = $"{DateTime.Now:MM - dd - yyyy} - {assignedToUser.Name}",
                AssignedTo = readingSheet.AssignedTo,
                BillingDate = readingSheet.BillingDate,
                DueDate = readingSheet.DueDate,
                DisconnectionDate = readingSheet.DisconnectionDate,
                BillingPeriodStart = readingSheet.BillingPeriodStart,
                SeqFrom = readingSheet.SeqFrom,
                SeqTo = readingSheet.SeqTo,
                ZoneBookId = zoneBook.Id
            };

            var savedSheet = await _readingSheetTrans.SaveUpdate(
                _user.User.Id,
                sheet);

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
            try
            {
                var readingSheets = await _readingSheetTrans.GetAll();

                if (readingSheets == null || !readingSheets.Any())
                    return Ok(new List<object>());

                // Get all required IDs
                var userIds = readingSheets
                    .Select(x => (int)x.AssignedTo)  // Convert long to int
                    .Distinct()
                    .ToList();

                var readingSheetIds = readingSheets
                    .Select(x => (long)x.Id)
                    .ToList();

                var zoneBookIds = readingSheets
                    .Select(x => x.ZoneBookId)
                    .Distinct()
                    .ToList();

                // DEBUG: Log user IDs being requested
                try
                {
                    System.IO.File.AppendAllText(
                        @"C:\Users\DESKTOP GSO-6\TMCWD\debug.txt",
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] User IDs requested: {string.Join(", ", userIds)}\n"
                    );
                }
                catch { }

                // Load related data
                var usersTask = _userTrans.GetUsersById(userIds);
                var readingsTask = _readingTransaction.GetRangeByReadingSheetIds(readingSheetIds);
                var zoneBooksTask = _zoneBookTrans.GetByIds(zoneBookIds);

                await Task.WhenAll(
                    usersTask,
                    readingsTask,
                    zoneBooksTask
                );

                var users = usersTask.Result ?? new List<User>();
                var readings = readingsTask.Result ?? new List<Reading>();
                var zoneBooks = zoneBooksTask.Result ?? new List<ZoneBook>();

                // DEBUG: Log loaded data
                try
                {
                    System.IO.File.AppendAllText(
                        @"C:\Users\DESKTOP GSO-6\TMCWD\debug.txt",
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Users loaded: {users.Count}\n" +
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Readings loaded: {readings.Count}\n" +
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ZoneBooks loaded: {zoneBooks.Count}\n"
                    );
                }
                catch { }

            // DEBUG: Log the readings count and users loaded
            try
            {
                System.IO.File.AppendAllText(
                    @"C:\Users\DESKTOP GSO-6\TMCWD\debug.txt",
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Total readings loaded: {readings.Count}\n" +
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Reading sheet IDs requested: {string.Join(", ", readingSheetIds)}\n" +
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Users loaded: {users.Count()} - IDs: {string.Join(", ", users.Select(u => u.Id))}\n"
                );
            }
            catch { }

            var result = new List<object>();

            foreach (var sheet in readingSheets)
            {
                var reader = users.FirstOrDefault(
                    x => x.Id == sheet.AssignedTo
                );

                var zoneBook = zoneBooks.FirstOrDefault(
                    x => x.Id == sheet.ZoneBookId
                );

                // Readings belonging to this reading sheet
                var sheetReadings = readings
                    .Where(x => x.ReadingSheetId == sheet.Id)
                    .ToList();

                var totalAccounts = sheetReadings.Count;

                var totalCompleted = sheetReadings.Count(
                    x => x.Status == ReadingStatus.Completed
                );

                var totalInProgress = sheetReadings.Count(
                    x => x.Status == ReadingStatus.InProgress
                );

                // DEBUG: Log per sheet to file
                try
                {
                    System.IO.File.AppendAllText(
                        @"C:\Users\DESKTOP GSO-6\TMCWD\debug.txt",
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Sheet {sheet.Id}: {sheetReadings.Count} readings, {totalCompleted} completed, {totalInProgress} in progress\n"
                    );
                }
                catch { }

                result.Add(new
                {
                    id = sheet.Id,
                    name = sheet.Name,

                    meterReader = reader?.Name ?? $"User {sheet.AssignedTo}",

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

                    // IMPORTANT
                    totalAccounts = totalAccounts,
                    totalCompleted = totalCompleted,
                    totalInProgress = totalInProgress,

                    forPosting = totalCompleted,

                    status = sheet.Status,

                    createdBy = sheet.CreatedBy,
                    dateCreated = sheet.DateCreated,
                    dateUpload = sheet.DateUpload
                });
            }

            return Ok(result);
            }
            catch (Exception ex)
            {
                // DEBUG: Log error
                try
                {
                    System.IO.File.AppendAllText(
                        @"C:\Users\DESKTOP GSO-6\TMCWD\debug.txt",
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR in GetAllReadingSheet: {ex.Message}\n{ex.StackTrace}\n"
                    );
                }
                catch { }
                return StatusCode(500, "Internal server error");
            }
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

        [HttpPost]
        public async Task<IActionResult> SaveReadingSheetTemplate(
            [FromBody] SaveTemplateRequest request)
        {
            if (request == null)
                return BadRequest("Invalid template request.");

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Template name is required.");

            if (request.ReaderId <= 0)
                return BadRequest("Meter reader is required.");

            if (request.Zone <= 0)
                return BadRequest("Zone is required.");

            if (request.Book <= 0)
                return BadRequest("Book is required.");

            var zoneBook = await _zoneBookTrans.GetByZoneAndBook(
                request.Zone,
                request.Book
            );

            if (zoneBook == null)
                return BadRequest("Zone/Book combination not found.");

            var template = new ReadingSheetTemplate
            {
                Id = request.Id,
                Name = request.Name,
                ReaderId = request.ReaderId,
                ZoneBookId = zoneBook.Id,
                IsActive = request.IsActive
            };

            var savedTemplate = await _readingSheetTemplateTrans.SaveUpdate(
                _user.User.Id,
                template
            );

            if (savedTemplate == null)
                return BadRequest("Failed to save reading sheet template.");

            return Ok(savedTemplate);
        }

        [HttpPost]
        public async Task<IActionResult> DeactivateReadingSheetTemplate(int id)
        {
            var template = await _readingSheetTemplateTrans.Deactivate(id, _user.User.Id);

            if (template == null)
                return NotFound();

            return Ok(template);
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

        [HttpDelete]
        public async Task<IActionResult> DeleteReadingSheet(int id)
        {
            var readingSheet = await _readingSheetTrans.Get(id);

            if (readingSheet == null)
                return NotFound();

            List<int> listIds = new();
            listIds.Add((int)readingSheet.Id); readingSheet.Status = ReadingStatus.Deleted;

            Task<ReadingSheet> updateReadingSheetTask = _readingSheetTrans.SaveUpdate(_user.User.Id, readingSheet);
            Task<List<Reading>> updateStatusByReadingSheetIdTask = _readingTransaction.UpdateStatusByReadingSheetIds(listIds, ReadingStatus.Deleted, _user.User.Id);

            await Task.WhenAll(updateReadingSheetTask,updateStatusByReadingSheetIdTask);

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

        [HttpPost]
        public async Task<IActionResult> UpdateReadingSheetsStatus([FromBody] UpdateSheetsStatusRequest request)
        {
            if (request == null || request.Ids == null || !request.Ids.Any())
            {
                return BadRequest(new { message = "Invalid request - sheet IDs required" });
            }

            var updatedSheets = await _readingSheetTrans.UpdateReadingSheetsStatus(
                request.Ids.Select(id => (int)id).ToList(),
                _user.User.Id,
                (ReadingStatus)request.Status
            );

            return Ok(new { 
                message = "Sheets status updated successfully", 
                count = updatedSheets.Count() 
            });
        }

        [HttpPost]
        public async Task<IActionResult> BulkUpdateStatus([FromBody] UpdateSheetsStatusRequest request)
        {
            if (request == null || request.Ids == null || !request.Ids.Any())
            {
                return BadRequest(new { message = "Invalid request - sheet IDs required" });
            }

            try
            {
                var updatedSheets = await _readingSheetTrans.UpdateReadingSheetsStatus(
                    request.Ids.Select(id => (int)id).ToList(),
                    _user.User.Id,
                    (ReadingStatus)request.Status
                );

                if (updatedSheets == null)
                {
                    return StatusCode(500, new { message = "Failed to update sheets - returned null" });
                }

                return Ok(new { 
                    message = "Sheets status updated successfully", 
                    count = updatedSheets.Count 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating sheets", error = ex.Message });
            }
        }



        [HttpGet]
        public async Task<IActionResult> GetReadingSheetAccounts(int readingSheetId)
        {
            var accounts = await _readingSheetTrans.GetReadingSheetAccounts(readingSheetId);

            if (accounts == null)
                return Ok(new List<object>());

            foreach (var acct in accounts)
            {
                if (acct.Pres <= 0 && acct.Prev <= 0)
                    continue; // no reading yet — leave amount at 0

                var amount = await WaterChargeCalculator.ComputeWaterCharge(
                    _waterRateTrans,
                    (int)acct.Prev,
                    (int)acct.Pres,
                    (AccountClassification)acct.Classification,
                    acct.MeterSize);

                if (amount >= 0)
                {
                    acct.Amount = amount;
                    acct.Total = acct.Balance + amount;
                }
            }

            return Ok(accounts);
        }
    }

    public class UpdateSheetsStatusRequest
    {
        public List<long> Ids { get; set; } = new List<long>();
        public int Status { get; set; }
    }
}
