using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TMCWD.Data.Context;
using TMCWD.Model.Dashboard;

namespace TMCWD.Data.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly UserDbContext _context;

    public DashboardController(UserDbContext context) => _context = context;

    private IQueryable<ServiceRequestOrderRecord> ServiceRequestRows() =>
        from request in _context.Requests.AsNoTracking()
        join detail in _context.RequestDetails.AsNoTracking() on request.Id equals detail.RequestId
        join order in _context.JobOrders.AsNoTracking() on detail.Id equals order.RequestDetailId
        where request.IsServiceRequest && order.RequestId == request.Id
        select new ServiceRequestOrderRecord
        {
            RequestId = request.Id,
            IsNonCustomer = request.IsNonCustomer,
            AccountId = request.AccountId,
            AccountNumber = request.AccountNumber,
            AccountName = request.AccountName,
            Address = request.Address,
            ReportedBy = request.ReportedBy,
            ContactNumber = request.ContactNumber,
            Source = request.Source,
            Name = request.Name,
            Particulars = request.Particulars ?? string.Empty,
            JobOrderNumber = order.JobOrderNumber,
            JobOrderDate = order.JobOrderDate ?? order.DateCreated,
            JobOrderType = detail.JobOrderType,
            JobDescription = detail.JobDescription,
            ReferredToUserId = order.ReferredToUserId,
            DateReferred = order.DateReferred,
            ReceivedByName = _context.Users.Where(x => x.Id == request.CreatedBy).Select(x => x.Name).FirstOrDefault() ?? string.Empty,
            Status = order.Status == (int)JobOrderStatus.Completed ? "Completed" : "Pending",
            IsReadOnly = request.Status == (int)RequestStatus.Completed
                || request.Status == (int)RequestStatus.Rejected
                || order.Status == (int)JobOrderStatus.Completed
                || order.Status == (int)JobOrderStatus.Rejected
        };

    private async Task CompleteServiceRequestHeaders(List<int> requestIds)
    {
        if (requestIds.Count == 0) return;
        // Include tracked changes made during installation reconciliation.
        var orders = await _context.JobOrders.Where(x => requestIds.Contains(x.RequestId)).ToListAsync();
        var requests = await _context.Requests.Where(x => requestIds.Contains((int)x.Id) && x.IsServiceRequest).ToListAsync();
        foreach (var request in requests)
        {
            if (orders.Where(x => x.RequestId == request.Id).All(x => x.Status == (int)JobOrderStatus.Completed))
            {
                request.Status = (int)RequestStatus.Completed;
                request.DateUpdated = DateTime.Now;
            }
        }
    }

    [HttpGet("ServiceSummary")]
    public async Task<ActionResult<ServiceDashboard>> ServiceSummary()
    {
        await BackfillApplicationAccountNumbers();
        await ReconcilePaidApplicationPayments();

        // Count applications by stage from water_connection_applications
        var stageCounts = await _context.WaterConnectionApplications.AsNoTracking()
            .GroupBy(x => x.Stage)
            .Select(x => new { Stage = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Stage, x => x.Count, StringComparer.OrdinalIgnoreCase);

        // Total active (not done/rejected)
        var total = stageCounts
            .Where(x => !x.Key.Equals("done", StringComparison.OrdinalIgnoreCase)
                     && !x.Key.Equals("rejected", StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.Value);

        await ReconcileCompletedServiceRequests();
        var summary = await ServiceRequestRows()
            .Where(x => x.Status == "Pending" && !x.IsReadOnly)
            .GroupBy(x => x.JobDescription)
            .OrderByDescending(g => g.Count())
            .Select(g => new ServiceRequestSummary
            {
                Request = g.Key,
                Count   = g.Count()
            }).ToListAsync();

        return Ok(new ServiceDashboard
        {
            OnlineApplications = stageCounts.GetValueOrDefault("application"),
            NewRequests        = stageCounts.GetValueOrDefault("application"),
            Inspection         = stageCounts.GetValueOrDefault("inspection"),
            Charging           = stageCounts.GetValueOrDefault("charging"),
            Payment            = stageCounts.GetValueOrDefault("payment"),
            Installation       = stageCounts.GetValueOrDefault("installation"),
            Connect            = stageCounts.GetValueOrDefault("connect"),
            Tapping            = stageCounts.GetValueOrDefault("tapping"),
            Tickets            = total,
            RequestSummary     = summary
        });
    }

    [HttpGet("ServiceApplications/{category}")]
    public async Task<ActionResult<List<ServiceApplicationRecord>>> ServiceApplications(string category)
    {
        category = category.Trim().ToLowerInvariant();
        var allowed = new[] { "applications", "new", "inspection", "charging", "payment", "installation", "connect", "tapping", "tickets" };
        if (!allowed.Contains(category)) return BadRequest("Unknown dashboard category.");

        await BackfillApplicationAccountNumbers();
        await ReconcilePaidApplicationPayments();

        // Map dashboard card name → stage value(s) in water_connection_applications
        var stageFilter = category switch
        {
            "new"          => new List<string> { "application" },
            "applications" => new List<string> { "application" },
            "inspection"   => new List<string> { "inspection" },
            "charging"     => new List<string> { "charging" },
            "payment"      => new List<string> { "payment" },
            "installation" => new List<string> { "installation" },
            "connect"      => new List<string> { "connect" },
            "tapping"      => new List<string> { "tapping" },
            "tickets"      => new List<string> { "application", "inspection", "charging", "payment", "installation", "connect", "tapping" },
            _              => (List<string>?)null
        };

        var query = _context.WaterConnectionApplications.AsNoTracking().AsQueryable();
        if (stageFilter != null)
            query = query.Where(x => stageFilter.Contains(x.Stage));

        var records = await query
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Select(x => new ServiceApplicationRecord
            {
                RequestId      = x.Id,
                AccountId      = x.AccountId,
                ControlNumber  = x.TrackingNumber,
                AccountNumber  = x.AccountNumber ?? string.Empty,
                AccountName    = x.FullName,
                Address        = x.Address + " " + x.Barangay,
                ContactNumber  = x.ContactNumber,
                Source         = x.Source,
                ServiceName    = x.ConnectionType + " connection",
                Stage          = x.Stage,
                DateSubmitted  = x.CreatedAt ?? DateTime.MinValue
            }).ToListAsync();

        return Ok(records);
    }

    [HttpPost("NewConnection/{userId:long}")]
    public async Task<ActionResult<NewConnectionApplicationRecord>> NewConnection(
        long userId, [FromBody] NewConnectionApplicationRecord record)
    {
        if (userId <= 0) return Unauthorized();
        record.ApplicantName = record.ApplicantName.Trim();
        record.Barangay = record.Barangay.Trim();
        record.Address = record.Address.Trim();
        record.MobileNumber = record.MobileNumber.Trim();
        record.ConsumerType = record.ConsumerType.Trim().ToLowerInvariant();

        if (record.ApplicantName.Length == 0) return BadRequest("Applicant name is required.");
        if (record.Barangay.Length == 0) return BadRequest("Barangay is required.");
        if (record.Address.Length == 0) return BadRequest("Service address is required.");
        if (record.MobileNumber.Length == 0) return BadRequest("Mobile number is required.");
        if (record.ConsumerType is not ("residential" or "commercial" or "commercialA" or "commercialB"
            or "commercialC" or "government" or "industrial" or "wholesale" or "bulk"))
            return BadRequest("Invalid consumer type.");

        var now = DateTime.Now;
        var application = new Entities.WaterConnectionApplication
        {
            TrackingNumber = $"PENDING-{Guid.NewGuid():N}",
            FullName = string.IsNullOrWhiteSpace(record.Suffix)
                ? record.ApplicantName
                : $"{record.ApplicantName} {record.Suffix.Trim()}",
            // Store the combined address string in Address for display purposes
            Address = string.Join(", ", new[] {
                record.UnitNumber?.Trim(), record.Building?.Trim(),
                record.HouseNumber?.Trim(), record.Street?.Trim()
            }.Where(x => !string.IsNullOrWhiteSpace(x))),
            Barangay    = record.Barangay,
            UnitNumber  = record.UnitNumber?.Trim(),
            Building    = record.Building?.Trim(),
            HouseNumber = record.HouseNumber?.Trim(),
            Street      = record.Street?.Trim(),
            ContactNumber = record.MobileNumber,
            Email = record.EmailAddress.Trim(),
            ConnectionType = record.ConsumerType,
            Source = "walk_in",
            Stage = "application",
            AssignedTo = userId,
            Remarks = JsonSerializer.Serialize(record),
            CreatedAt = record.DateApplied?.Date ?? now,
            UpdatedAt = now
        };

        await using var transaction = await _context.Database.BeginTransactionAsync();
        _context.WaterConnectionApplications.Add(application);
        await _context.SaveChangesAsync();
        application.TrackingNumber = $"APP-{now:yyyy}-{application.Id:D6}";
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        record.ApplicationId = application.Id;
        record.TrackingNumber = application.TrackingNumber;
        record.DateApplied = application.CreatedAt;
        return Ok(record);
    }

    [HttpGet("DisconnectionAccounts")]
    public async Task<ActionResult<List<DisconnectionAccountRecord>>> DisconnectionAccounts([FromQuery] string q)
    {
        q = (q ?? string.Empty).Trim().ToLowerInvariant();
        if (q.Length < 2) return Ok(new List<DisconnectionAccountRecord>());

        var matches = await (from account in _context.Accounts.AsNoTracking()
                             join customer in _context.Customers.AsNoTracking() on account.CustomerId equals customer.Id
                             where account.AccountNumber.ToLower().Contains(q)
                                || account.MeterNumber.ToLower().Contains(q)
                                || customer.Firstname.ToLower().Contains(q)
                                || customer.Lastname.ToLower().Contains(q)
                             orderby account.AccountNumber
                             select new { account, customer }).Take(20).ToListAsync();
        var accountIds = matches.Select(x => x.account.Id).ToList();
        var readings = await _context.Readings.AsNoTracking()
            .Where(x => accountIds.Contains(x.AccountId))
            .OrderByDescending(x => x.DateCreated).ThenByDescending(x => x.Id)
            .ToListAsync();
        var latest = readings.GroupBy(x => x.AccountId).ToDictionary(x => x.Key, x => (decimal?)x.First().CurrentReading);

        return Ok(matches.Select(x => new DisconnectionAccountRecord
        {
            AccountId = x.account.Id,
            AccountNumber = x.account.AccountNumber,
            FullName = string.Join(" ", new[] { x.customer.Firstname, x.customer.Middlename, x.customer.Lastname }.Where(y => !string.IsNullOrWhiteSpace(y))),
            Address = string.Join(", ", new[]
            {
                string.Join(" ", new[] { x.account.UnitNumber, x.account.Building, x.account.HouseNumber, x.account.Street }.Where(y => !string.IsNullOrWhiteSpace(y))),
                x.account.Barangay
            }.Where(y => !string.IsNullOrWhiteSpace(y))),
            ContactNumber = x.customer.PhoneNumber ?? string.Empty,
            MeterNumber = x.account.MeterNumber,
            AccountStatus = x.account.Status switch { 1 => "Pending", 2 => "Suspended", 3 => "Active", 4 => "Closed", _ => "Unknown" },
            LastReading = latest.GetValueOrDefault(x.account.Id)
        }).ToList());
    }

    [HttpGet("DisconnectionOrders")]
    public async Task<ActionResult<List<DisconnectionOrderRecord>>> DisconnectionOrders()
    {
        var rows = await (from order in _context.DisconnectionOrders.AsNoTracking()
                          join account in _context.Accounts.AsNoTracking() on order.AccountId equals account.Id
                          join customer in _context.Customers.AsNoTracking() on account.CustomerId equals customer.Id
                          orderby order.JobOrderDate descending, order.Id descending
                          select new DisconnectionOrderRecord
                          {
                              OrderId = order.Id, AccountId = account.Id, JobOrderNumber = order.JobOrderNumber,
                              JobOrderDate = order.JobOrderDate, AccountNumber = account.AccountNumber,
                              FullName = customer.Firstname + " " + customer.Middlename + " " + customer.Lastname,
                              MeterNumber = account.MeterNumber, Status = order.Status,
                              DisconnectionDate = order.DisconnectionDate, NatureOfDisconnection = order.NatureOfDisconnection,
                              Possession = order.Possession, Reason = order.Reason, LastReading = order.LastReading,
                              Plumber = order.Plumber, ReconnectionFee = order.ReconnectionFee, Remarks = order.Remarks,
                              ChargeFee = order.ChargeFee, ReadingOverride = order.ReadingOverride
                          }).Take(100).ToListAsync();
        var accountIds = rows.Select(x => x.AccountId).Distinct().ToList();
        var addressRows = await _context.Accounts.AsNoTracking().Where(x => accountIds.Contains(x.Id)).ToListAsync();
        var addresses = addressRows.ToDictionary(x => x.Id, x => string.Join(", ", new[]
        {
            string.Join(" ", new[] { x.UnitNumber, x.Building, x.HouseNumber, x.Street }.Where(y => !string.IsNullOrWhiteSpace(y))),
            x.Barangay
        }.Where(y => !string.IsNullOrWhiteSpace(y))));
        foreach (var row in rows) row.Address = addresses.GetValueOrDefault(row.AccountId, string.Empty);
        return Ok(rows);
    }

    [HttpPost("DisconnectionOrder/{userId:long}")]
    public async Task<ActionResult<DisconnectionOrderRecord>> SaveDisconnectionOrder(
        long userId, [FromBody] DisconnectionOrderRecord record)
    {
        if (userId <= 0) return Unauthorized();
        var action = (record.Action ?? string.Empty).Trim().ToLowerInvariant();
        if (action is not ("disconnect" or "accomplish" or "cancel")) return BadRequest("Unknown disconnection action.");

        var account = await _context.Accounts.FirstOrDefaultAsync(x => x.Id == record.AccountId);
        if (account == null) return NotFound("Account was not found.");
        if (record.DisconnectionDate == null) return BadRequest("Disconnection date is required.");
        if (string.IsNullOrWhiteSpace(record.NatureOfDisconnection)) return BadRequest("Nature of disconnection is required.");
        if (string.IsNullOrWhiteSpace(record.Reason)) return BadRequest("Reason is required.");

        var now = DateTime.Now;
        var order = record.OrderId > 0
            ? await _context.DisconnectionOrders.FirstOrDefaultAsync(x => x.Id == record.OrderId)
            : null;
        if (record.OrderId > 0 && order == null) return NotFound("Disconnection order was not found.");
        if (order == null && action != "disconnect") return BadRequest("Create the disconnection order before completing or cancelling it.");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        if (order == null)
        {
            order = new Entities.DisconnectionOrder
            {
                AccountId = account.Id,
                JobOrderNumber = $"PENDING-{Guid.NewGuid():N}",
                JobOrderDate = record.JobOrderDate ?? now,
                ProcessedBy = userId,
                CreatedAt = now
            };
            _context.DisconnectionOrders.Add(order);
        }
        order.DisconnectionDate = record.DisconnectionDate;
        order.NatureOfDisconnection = record.NatureOfDisconnection.Trim();
        order.Possession = record.Possession.Trim();
        order.Reason = record.Reason.Trim();
        order.LastReading = record.LastReading;
        order.Plumber = record.Plumber.Trim();
        order.ReconnectionFee = record.ReconnectionFee;
        order.Remarks = record.Remarks.Trim();
        order.ChargeFee = record.ChargeFee;
        order.ReadingOverride = record.ReadingOverride;
        order.Status = action switch { "accomplish" => "Accomplished", "cancel" => "Cancelled", _ => "For Disconnection" };
        order.UpdatedAt = now;
        await _context.SaveChangesAsync();
        if (order.JobOrderNumber.StartsWith("PENDING-", StringComparison.Ordinal))
        {
            order.JobOrderNumber = $"DCO-{now:yyyy}-{order.Id:D6}";
            await _context.SaveChangesAsync();
        }
        if (action == "accomplish") account.Status = (int)AccountStatus.Suspended;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        record.OrderId = order.Id;
        record.JobOrderNumber = order.JobOrderNumber;
        record.JobOrderDate = order.JobOrderDate;
        record.AccountNumber = account.AccountNumber;
        record.MeterNumber = account.MeterNumber;
        record.Status = order.Status;
        return Ok(record);
    }

    [HttpGet("AccountTransferAccounts")]
    public async Task<ActionResult<List<AccountTransferLookupRecord>>> AccountTransferAccounts([FromQuery] string q)
    {
        q = (q ?? string.Empty).Trim().ToLowerInvariant();
        if (q.Length < 2) return Ok(new List<AccountTransferLookupRecord>());

        var matches = await (from account in _context.Accounts.AsNoTracking()
                             join customer in _context.Customers.AsNoTracking() on account.CustomerId equals customer.Id
                             join zoneBook in _context.ZoneBooks.AsNoTracking() on account.ZoneBookId equals zoneBook.Id
                             where account.AccountNumber.ToLower().Contains(q)
                                || customer.Firstname.ToLower().Contains(q)
                                || customer.Lastname.ToLower().Contains(q)
                             orderby account.AccountNumber
                             select new { account, customer, zoneBook }).Take(20).ToListAsync();

        return Ok(matches.Select(x => new AccountTransferLookupRecord
        {
            AccountId = x.account.Id,
            AccountNumber = x.account.AccountNumber,
            AccountName = string.Join(" ", new[] { x.customer.Firstname, x.customer.Middlename, x.customer.Lastname }.Where(y => !string.IsNullOrWhiteSpace(y))),
            Address = string.Join(", ", new[]
            {
                string.Join(" ", new[] { x.account.UnitNumber, x.account.Building, x.account.HouseNumber, x.account.Street }.Where(y => !string.IsNullOrWhiteSpace(y))),
                x.account.Barangay
            }.Where(y => !string.IsNullOrWhiteSpace(y))),
            Zone = x.zoneBook.Zone,
            Book = x.zoneBook.Book
        }).ToList());
    }

    [HttpPost("AccountTransfer/{userId:long}")]
    public async Task<ActionResult<AccountTransferRecord>> AccountTransfer(
        long userId, [FromBody] AccountTransferRecord record)
    {
        if (userId <= 0) return Unauthorized();
        var account = await _context.Accounts.FirstOrDefaultAsync(x => x.Id == record.AccountId);
        if (account == null) return NotFound("Account was not found.");

        record.NewAccountNumber = (record.NewAccountNumber ?? string.Empty).Trim();
        if (record.NewAccountNumber.Length == 0) return BadRequest("New account number is required.");
        if (record.NewAccountNumber.Length > 50) return BadRequest("New account number cannot exceed 50 characters.");
        if (record.NewAccountNumber.Equals(account.AccountNumber, StringComparison.OrdinalIgnoreCase))
            return BadRequest("The new account number must be different from the current account number.");
        if (record.TransferDate == null) return BadRequest("Transfer date is required.");

        var duplicate = await _context.Accounts.AsNoTracking().AnyAsync(x => x.Id != account.Id && x.AccountNumber.ToLower() == record.NewAccountNumber.ToLower());
        if (duplicate) return Conflict("The new account number is already assigned to another account.");
        var zoneBook = await _context.ZoneBooks.FirstOrDefaultAsync(x => x.Zone == record.Zone && x.Book == record.Book);
        if (zoneBook == null) return BadRequest("Select a valid zone and book.");

        var now = DateTime.Now;
        var order = new Entities.AccountTransferOrder
        {
            AccountId = account.Id,
            JobOrderNumber = $"PENDING-{Guid.NewGuid():N}",
            JobOrderDate = record.JobOrderDate ?? now,
            OldAccountNumber = account.AccountNumber,
            NewAccountNumber = record.NewAccountNumber,
            Zone = record.Zone,
            Book = record.Book,
            TransferDate = record.TransferDate.Value,
            Status = "Completed",
            ProcessedBy = userId,
            CreatedAt = now,
            UpdatedAt = now
        };

        await using var transaction = await _context.Database.BeginTransactionAsync();
        _context.AccountTransferOrders.Add(order);
        await _context.SaveChangesAsync();
        order.JobOrderNumber = $"ATO-{now:yyyy}-{order.Id:D6}";
        account.AccountNumber = record.NewAccountNumber;
        account.ZoneBookId = checked((int)zoneBook.Id);
        account.UpdatedBy = userId;
        account.DateUpdated = now;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        record.TransferId = order.Id;
        record.JobOrderNumber = order.JobOrderNumber;
        record.JobOrderDate = order.JobOrderDate;
        record.AccountNumber = order.OldAccountNumber;
        record.Status = order.Status;
        return Ok(record);
    }

    [HttpGet("ServiceRequestAssignees")]
    public async Task<ActionResult<List<ServiceRequestAssigneeRecord>>> ServiceRequestAssignees()
    {
        var users = await _context.Users.AsNoTracking()
            .Where(x => x.IsActive == true)
            .OrderBy(x => x.Name)
            .Select(x => new ServiceRequestAssigneeRecord
            {
                UserId = x.Id,
                Name = x.Name ?? string.Empty
            }).ToListAsync();
        return Ok(users);
    }

    [HttpGet("ServiceRequestsByDescription")]
    public async Task<ActionResult<List<object>>> ServiceRequestsByDescription([FromQuery] string? description, [FromQuery] string? status = null)
    {
        if (status != null && status != "Pending" && status != "Completed")
            return BadRequest("Select Pending or Completed.");
        if (string.IsNullOrWhiteSpace(description) && status != "Completed")
            return BadRequest("Description is required.");

        await ReconcileCompletedServiceRequests();
        var rows = await ServiceRequestRows()
            .Where(x => string.IsNullOrWhiteSpace(description) || x.JobDescription == description.Trim())
            .Where(x => status == null || x.Status == status)
            .Where(x => status != "Pending" || !x.IsReadOnly)
            .OrderByDescending(x => x.JobOrderDate)
            .Select(x => new
            {
                source = x.Source,
                receivedByName = x.ReceivedByName,
                referredToName = x.ReferredToUserId == -1 ? "Billing" :
                    x.ReferredToUserId == -2 ? "Maintenance" :
                    x.ReferredToUserId == -3 ? "Customer Service" :
                    _context.Users.Where(u => u.Id == x.ReferredToUserId).Select(u => u.Name).FirstOrDefault(),
                dateReferred = x.DateReferred,
                name = x.Name,
                particulars = x.Particulars,
                requestId       = x.RequestId,
                jobOrderNumber  = x.JobOrderNumber,
                jobOrderDate    = x.JobOrderDate,
                accountNumber   = x.AccountNumber,
                meterNumber     = _context.Accounts.Where(a => a.Id == x.AccountId).Select(a => a.MeterNumber).FirstOrDefault(),
                lastReading     = _context.Readings.Where(r => r.AccountId == x.AccountId && r.IsCompleted && r.DateCreated <= x.JobOrderDate)
                    .OrderByDescending(r => r.DateCreated).ThenByDescending(r => r.Id)
                    .Select(r => (decimal?)r.CurrentReading).FirstOrDefault(),
                accountName     = x.AccountName,
                address         = x.Address,
                reportedBy      = x.ReportedBy,
                contactNumber   = x.ContactNumber,
                jobOrderType    = x.JobOrderType,
                jobDescription  = x.JobDescription,
                status          = x.Status,
                isReadOnly      = x.IsReadOnly
            })
            .ToListAsync();

        return Ok(rows);
    }

    private async Task ReconcileCompletedServiceRequests()
    {
        var snapshots = await _context.WaterConnectionApplications.AsNoTracking().Where(x => x.Stage == "done" && x.InstallationData != null).Select(x => x.InstallationData).ToListAsync();
        var numbers = new HashSet<string>();
        foreach (var snapshot in snapshots)
        {
            try
            {
                var record = JsonSerializer.Deserialize<ServiceRequestOrderRecord>(snapshot!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (record != null && !string.IsNullOrWhiteSpace(record.JobOrderNumber)) numbers.Add(record.JobOrderNumber);
            }
            catch (JsonException) { }
        }
        if (numbers.Count == 0) return;
        var orders = await (from order in _context.JobOrders
                            join request in _context.Requests on order.RequestId equals request.Id
                            where request.IsServiceRequest && numbers.Contains(order.JobOrderNumber)
                                && order.Status != (int)JobOrderStatus.Completed
                                && order.Status != (int)JobOrderStatus.Rejected
                            select order).ToListAsync();
        foreach (var order in orders) { order.Status = (int)JobOrderStatus.Completed; order.DateUpdated = DateTime.Now; }
        await CompleteServiceRequestHeaders(orders.Select(x => x.RequestId).Distinct().ToList());
        if (orders.Count > 0) await _context.SaveChangesAsync();
    }

    [HttpPost("SaveServiceRequest/{userId:long}")]
    public async Task<IActionResult> SaveServiceRequest(long userId, [FromBody] ServiceRequestOrderRecord record)
    {
        if (!await _context.Users.AnyAsync(x => x.Id == userId && x.IsActive == true)) return Unauthorized();
        var order = await _context.Requests.FirstOrDefaultAsync(x => x.Id == record.RequestId && x.IsServiceRequest);
        if (order == null) return NotFound("Service request was not found.");
        if (order.Status == (int)RequestStatus.Completed || order.Status == (int)RequestStatus.Rejected
            || !await _context.JobOrders.AnyAsync(x => x.RequestId == order.Id
                && x.Status != (int)JobOrderStatus.Completed && x.Status != (int)JobOrderStatus.Rejected))
            return Conflict("This service request is already finished and cannot be edited.");
        var source = (record.Source ?? string.Empty).Trim();
        var reporter = (record.ReportedBy ?? string.Empty).Trim();
        var contact = (record.ContactNumber ?? string.Empty).Trim();
        var name = (record.Name ?? string.Empty).Trim();
        var particulars = (record.Particulars ?? string.Empty).Trim();
        if (source.Length > 100 || reporter.Length > 200 || contact.Length > 50 || name.Length > 200 || particulars.Length > 2000)
            return BadRequest("One or more request details exceed the allowed length.");
        order.Source = source;
        order.ReportedBy = reporter;
        order.ContactNumber = contact;
        order.Name = name;
        order.Particulars = particulars;
        order.DateUpdated = DateTime.Now;
        order.UpdatedBy = userId;
        await _context.SaveChangesAsync();
        return Ok(new { source, reportedBy = reporter, contactNumber = contact, name, particulars, status = "Pending" });
    }

    [HttpPost("AccomplishServiceRequest/{requestId:long}/{userId:long}")]
    public async Task<IActionResult> AccomplishServiceRequest(long requestId, long userId)
    {
        if (!await _context.Users.AnyAsync(x => x.Id == userId && x.IsActive == true)) return Unauthorized();
        var request = await _context.Requests.FirstOrDefaultAsync(x => x.Id == requestId && x.IsServiceRequest);
        if (request == null) return NotFound("Service request was not found.");
        var orders = await _context.JobOrders.Where(x => x.RequestId == request.Id).ToListAsync();
        if (request.Status == (int)RequestStatus.Rejected
            || orders.Any(x => x.Status == (int)JobOrderStatus.Rejected))
            return Conflict("A cancelled request cannot be accomplished.");
        if (orders.Count == 0) return Conflict("This request has no job orders.");
        foreach (var order in orders)
        {
            order.Status = (int)JobOrderStatus.Completed;
            order.DateUpdated = DateTime.Now;
            order.UpdatedBy = checked((int)userId);
        }
        request.Status = (int)RequestStatus.Completed;
        request.DateUpdated = DateTime.Now;
        request.UpdatedBy = userId;
        await _context.SaveChangesAsync();
        return Ok(new { status = "Completed" });
    }

    [HttpPost("ServiceRequest/{userId:long}")]
    public async Task<ActionResult<ServiceRequestOrderRecord>> ServiceRequest(
        long userId, [FromBody] ServiceRequestOrderRecord record)
    {
        if (userId <= 0) return Unauthorized();
        var receivedBy = await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId && x.IsActive == true);
        if (receivedBy == null) return Unauthorized("The receiving user account is not active.");

        record.Address = (record.Address ?? string.Empty).Trim();
        record.ReportedBy = (record.ReportedBy ?? string.Empty).Trim();
        record.ContactNumber = (record.ContactNumber ?? string.Empty).Trim();
        record.Source = (record.Source ?? string.Empty).Trim();
        record.JobOrderType = (record.JobOrderType ?? string.Empty).Trim();
        record.JobDescription = (record.JobDescription ?? string.Empty).Trim();
        record.ReferredToDepartment = (record.ReferredToDepartment ?? string.Empty).Trim();
        record.Name = (record.Name ?? string.Empty).Trim();
        record.Particulars = (record.Particulars ?? string.Empty).Trim();

        if (record.JobOrderDate == null) return BadRequest("Job order date is required.");
        if (record.Source.Length == 0) return BadRequest("Source is required.");
        if (record.JobOrderType.Length == 0) return BadRequest("Job order type is required.");
        if (record.JobDescription.Length == 0) return BadRequest("Job description is required.");
        if (record.ReportedBy.Length == 0) return BadRequest("Reported by is required.");

        Entities.WaterConnectionApplication? installationApplication = null;
        if (record.ApplicationId > 0)
        {
            installationApplication = await _context.WaterConnectionApplications
                .FirstOrDefaultAsync(x => x.Id == record.ApplicationId);
            if (installationApplication == null) return NotFound("Installation application was not found.");
            if (!installationApplication.Stage.Equals("installation", StringComparison.OrdinalIgnoreCase))
                return Conflict("This application is no longer waiting for installation.");
            if (installationApplication.AccountId is null or <= 0)
                return BadRequest("The installation application has no linked account.");
            record.IsNonCustomer = false;
            record.AccountId = installationApplication.AccountId;
        }

        long? accountId = null;
        long? customerId = null;
        var accountNumber = string.Empty;
        var accountName = string.Empty;
        if (record.IsNonCustomer)
        {
            if (record.Address.Length == 0) return BadRequest("Address is required for a non-customer request.");
            if (record.ContactNumber.Length == 0) return BadRequest("Contact number is required for a non-customer request.");
        }
        else
        {
            if (record.AccountId is null or <= 0) return BadRequest("Select an existing account.");
            var source = await (from account in _context.Accounts.AsNoTracking()
                                join customer in _context.Customers.AsNoTracking() on account.CustomerId equals customer.Id
                                where account.Id == record.AccountId.Value
                                select new { account, customer }).FirstOrDefaultAsync();
            if (source == null) return NotFound("Account was not found.");
            accountId = source.account.Id;
            customerId = source.customer.Id;
            accountNumber = source.account.AccountNumber;
            accountName = string.Join(" ", new[] { source.customer.Firstname, source.customer.Middlename, source.customer.Lastname }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
            record.Address = string.Join(", ", new[]
            {
                string.Join(" ", new[] { source.account.UnitNumber, source.account.Building, source.account.HouseNumber, source.account.Street }
                    .Where(x => !string.IsNullOrWhiteSpace(x))),
                source.account.Barangay
            }.Where(x => !string.IsNullOrWhiteSpace(x)));
            record.ContactNumber = source.customer.PhoneNumber ?? string.Empty;
        }

        string referredToName = string.Empty;
        long? referredToId = null;
        if (record.ReferredToDepartment.Length > 0)
        {
            var departments = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
            {
                ["Billing"] = -1,
                ["Maintenance"] = -2,
                ["Customer Service"] = -3
            };
            if (!departments.TryGetValue(record.ReferredToDepartment, out var departmentId))
                return BadRequest("Select Billing, Maintenance, or Customer Service as the referral department.");
            referredToId = departmentId;
            referredToName = departments.Keys.First(x => x.Equals(record.ReferredToDepartment, StringComparison.OrdinalIgnoreCase));
            record.ReferredToDepartment = referredToName;
        }
        else if (record.ReferredToUserId is > 0)
        {
            var referred = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == record.ReferredToUserId.Value && x.IsActive == true);
            if (referred == null) return BadRequest("Select a valid referral recipient.");
            referredToId = referred.Id;
            referredToName = referred.Name ?? string.Empty;
        }

        var now = DateTime.Now;
        if (record.Address.Length > 500 || record.ReportedBy.Length > 200 || record.ContactNumber.Length > 50
            || record.Source.Length > 100 || record.JobOrderType.Length > 100 || record.JobDescription.Length > 255
            || record.Name.Length > 200 || record.Particulars.Length > 2000)
            return BadRequest("One or more request details exceed the allowed length.");

        var requestTypeId = await _context.InspectionTypes.AsNoTracking()
            .Where(x => x.Name == record.JobDescription || x.Name == "Service Request")
            .OrderByDescending(x => x.Name == record.JobDescription)
            .Select(x => x.Id).FirstOrDefaultAsync();
        if (requestTypeId == 0) return Conflict("The service request database migration has not been applied.");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var request = new Entities.Request
        {
            IsServiceRequest = true,
            IsNonCustomer = record.IsNonCustomer,
            CustomerId = customerId,
            AccountId = accountId,
            AccountNumber = accountNumber,
            AccountName = accountName,
            Address = record.Address,
            ReportedBy = record.ReportedBy,
            ContactNumber = record.ContactNumber,
            ControlNumber = $"PENDING-{Guid.NewGuid():N}",
            Source = record.Source,
            Name = record.Name,
            Particulars = record.Particulars,
            CreatedBy = userId,
            UpdatedBy = userId,
            Status = (int)RequestStatus.InProgress,
            DateCreated = now,
            DateUpdated = now
        };
        _context.Requests.Add(request);
        await _context.SaveChangesAsync();
        request.ControlNumber = $"SR-{now:yyyy}-{request.Id:D6}";
        var detail = new Entities.RequestDetail
        {
            RequestId = checked((int)request.Id), RequestTypeId = checked((int)requestTypeId),
            JobDescription = record.JobDescription, JobOrderType = record.JobOrderType,
            DateCreated = now, DateUpdated = now
        };
        _context.RequestDetails.Add(detail);
        await _context.SaveChangesAsync();
        var order = new Entities.JobOrder
        {
            RequestId = checked((int)request.Id), RequestDetailId = checked((int)detail.Id),
            JobOrderNumber = $"PENDING-{Guid.NewGuid():N}", JobOrderDate = record.JobOrderDate.Value,
            ReferredToUserId = referredToId, DateReferred = referredToId.HasValue ? record.DateReferred ?? now.Date : null,
            Status = (int)JobOrderStatus.Inspection, CreatedBy = checked((int)userId),
            UpdatedBy = checked((int)userId), DateCreated = now, DateUpdated = now
        };
        _context.JobOrders.Add(order);
        await _context.SaveChangesAsync();
        order.JobOrderNumber = $"JO-{now:yyyy}-{order.Id:D6}";

        record.RequestId = request.Id;
        record.AccountId = request.AccountId;
        record.AccountNumber = request.AccountNumber;
        record.AccountName = request.AccountName;
        record.JobOrderNumber = order.JobOrderNumber;
        record.ReferredToUserId = order.ReferredToUserId;
        record.ReferredToName = referredToName;
        record.ReceivedByName = receivedBy.Name ?? string.Empty;
        record.Status = "Pending";

        if (installationApplication != null)
        {
            installationApplication.InstallationData = JsonSerializer.Serialize(record);
            installationApplication.Stage = "connect";
            installationApplication.UpdatedAt = now;
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(record);
    }

    [HttpGet("ServiceInspection/{requestId:long}")]
    public async Task<ActionResult<ServiceInspectionRecord>> ServiceInspection(long requestId)
    {
        var application = await _context.WaterConnectionApplications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == requestId);
        if (application != null)
        {
            ServiceInspectionRecord applicationRecord;
            try { applicationRecord = JsonSerializer.Deserialize<ServiceInspectionRecord>(application.InspectionData ?? "{}", new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new(); }
            catch (JsonException) { applicationRecord = new(); }
            var activeUsers = await _context.Users.AsNoTracking().Where(x => x.IsActive == true).OrderBy(x => x.Name)
                .Select(x => new { x.Id, Name = x.Name ?? x.Email ?? "User", x.Role }).ToListAsync();
            applicationRecord.ReportId = application.Id; applicationRecord.RequestId = application.Id;
            applicationRecord.ApplicationNumber = application.TrackingNumber; applicationRecord.ApplicantName = application.FullName;
            applicationRecord.DateApplied = application.CreatedAt ?? DateTime.MinValue;
            applicationRecord.Address = string.Join(" ", new[] { application.Address, application.Barangay }.Where(x => !string.IsNullOrWhiteSpace(x)));
            applicationRecord.MobileNumber = application.ContactNumber; applicationRecord.Classification = application.ConnectionType;
            applicationRecord.ServiceName = application.ConnectionType + " connection";
            applicationRecord.Inspectors = activeUsers.Where(x => x.Role is (int)UserRole.Engineer or (int)UserRole.SuperAdmin)
                .Select(x => new ServiceInspectionOption { Value = x.Id, Label = x.Name }).ToList();
            applicationRecord.Approvers = activeUsers.Where(x => x.Role is (int)UserRole.Engineer or (int)UserRole.SuperAdmin or (int)UserRole.CustomerServiceOic)
                .Select(x => new ServiceInspectionOption { Value = x.Id, Label = x.Name }).ToList();
            applicationRecord.Zones = await _context.ZoneBooks.AsNoTracking().OrderBy(x => x.Zone).ThenBy(x => x.Book)
                .Select(x => new ServiceInspectionZone { Zone = x.Zone, Book = x.Book }).ToListAsync();
            // Echo back the account number if already activated
            if (!string.IsNullOrWhiteSpace(application.AccountNumber))
                applicationRecord.AccountNumber = application.AccountNumber;

            // Parse Zone/Book from the original application Remarks JSON so the
            // inspection form can display them as read-only fields.
            if (!string.IsNullOrWhiteSpace(application.Remarks))
            {
                try
                {
                    using var doc = JsonDocument.Parse(application.Remarks);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("Zone", out var z) && z.ValueKind == JsonValueKind.Number)
                        applicationRecord.ApplicationZone = z.GetInt32();
                    if (root.TryGetProperty("Book", out var b) && b.ValueKind == JsonValueKind.Number)
                        applicationRecord.ApplicationBook = b.GetInt32();
                    // Pre-fill meter size from the application's chosen service size
                    if (root.TryGetProperty("ServiceSize", out var ss) && ss.ValueKind == JsonValueKind.String)
                    {
                        var sizeStr = ss.GetString() ?? string.Empty;
                        // Parse "0.5", "0.75", "1", "1.5", "2", "3", "4" etc.
                        if (decimal.TryParse(sizeStr, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var sizeDecimal)
                            && sizeDecimal > 0)
                        {
                            applicationRecord.MeterSize = sizeDecimal;
                        }
                    }
                }
                catch (JsonException) { }
            }
            // Fall back to zone/book already stored in InspectionData if Remarks not available
            if (applicationRecord.ApplicationZone == null && applicationRecord.Zone.HasValue)
                applicationRecord.ApplicationZone = applicationRecord.Zone;
            if (applicationRecord.ApplicationBook == null && applicationRecord.Book.HasValue)
                applicationRecord.ApplicationBook = applicationRecord.Book;

            return Ok(applicationRecord);
        }
        var source = await (from request in _context.Requests.AsNoTracking()
                            join account in _context.Accounts.AsNoTracking() on request.AccountId equals account.Id
                            join customer in _context.Customers.AsNoTracking() on request.CustomerId equals customer.Id
                            where request.Id == requestId
                            select new { request, account, customer }).FirstOrDefaultAsync();
        if (source == null) return NotFound("Service application was not found.");

        var stored = await _context.InspectionReports.AsNoTracking()
            .Where(x => x.RequestId == requestId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();
        ServiceInspectionRecord record;
        try
        {
            record = stored == null
                ? new ServiceInspectionRecord()
                : JsonSerializer.Deserialize<ServiceInspectionRecord>(stored.Details,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new ServiceInspectionRecord();
        }
        catch (JsonException)
        {
            record = new ServiceInspectionRecord { InspectionRemarks = stored?.Details ?? string.Empty };
        }

        var serviceNames = await (from detail in _context.RequestDetails.AsNoTracking()
                                  join type in _context.InspectionTypes.AsNoTracking() on detail.RequestTypeId equals type.Id
                                  where detail.RequestId == requestId
                                  select type.Name).Distinct().ToListAsync();
        var users = await _context.Users.AsNoTracking()
            .Where(x => x.IsActive == true)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, Name = x.Name ?? x.Email ?? "User" , x.Role })
            .ToListAsync();

        record.ReportId = stored?.Id ?? 0;
        record.RequestId = requestId;
        record.ApplicationNumber = source.request.ControlNumber;
        record.ApplicantName = $"{source.customer.Lastname}, {source.customer.Firstname} {source.customer.Middlename}".Trim();
        record.DateApplied = source.request.DateCreated;
        record.Address = string.Join(" ", new[] { source.account.UnitNumber, source.account.Building,
            source.account.HouseNumber, source.account.Street, source.account.Barangay }.Where(x => !string.IsNullOrWhiteSpace(x)));
        record.MobileNumber = source.customer.PhoneNumber;
        record.Classification = Enum.IsDefined(typeof(AccountClassification), source.account.Classification)
            ? ((AccountClassification)source.account.Classification).ToString()
            : "Unclassified";
        record.ServiceName = string.Join(", ", serviceNames);
        record.Inspectors = users.Where(x => x.Role is (int)UserRole.Engineer or (int)UserRole.SuperAdmin)
            .Select(x => new ServiceInspectionOption { Value = x.Id, Label = x.Name }).ToList();
        record.Approvers = users.Where(x => x.Role is (int)UserRole.Engineer or (int)UserRole.SuperAdmin or (int)UserRole.CustomerServiceOic)
            .Select(x => new ServiceInspectionOption { Value = x.Id, Label = x.Name }).ToList();
        record.Zones = await _context.ZoneBooks.AsNoTracking().OrderBy(x => x.Zone).ThenBy(x => x.Book)
            .Select(x => new ServiceInspectionZone { Zone = x.Zone, Book = x.Book }).ToListAsync();
        return Ok(record);
    }

    [HttpPost("SaveServiceInspection/{userId:long}")]
    public async Task<ActionResult<ServiceInspectionRecord>> SaveServiceInspection(long userId, [FromBody] ServiceInspectionRecord record)
    {
        var application = await _context.WaterConnectionApplications.FirstOrDefaultAsync(x => x.Id == record.RequestId);
        if (application != null)
        {
            if (!record.DateInspected.HasValue) return BadRequest("Date inspected is required.");
            if (!record.InspectedByUserId.HasValue) return BadRequest("Inspector is required.");
            record.InspectedByName = await _context.Users.Where(x => x.Id == record.InspectedByUserId).Select(x => x.Name ?? x.Email ?? string.Empty).FirstOrDefaultAsync() ?? string.Empty;
            record.ApprovedByName = record.ApprovedByUserId.HasValue ? await _context.Users.Where(x => x.Id == record.ApprovedByUserId).Select(x => x.Name ?? x.Email ?? string.Empty).FirstOrDefaultAsync() ?? string.Empty : string.Empty;
            record.Inspectors = new(); record.Approvers = new(); record.Zones = new();
            application.InspectionData = JsonSerializer.Serialize(record);
            application.UpdatedAt = DateTime.Now;

            // ── Auto-activate account when the inspection is Approved ────────
            // The account number is generated here so it can be printed on the
            // inspection report PDF before the maintenance team goes to the field.
            bool isApproval = record.Status?.Equals("Approved", StringComparison.OrdinalIgnoreCase) == true
                           && record.ApprovedByUserId.HasValue;
            bool isDisapproved = record.Status?.Equals("Disapproved", StringComparison.OrdinalIgnoreCase) == true;

            if (isApproval && string.IsNullOrWhiteSpace(application.AccountNumber))
            {
                // Use zone/book from the application's own data (ApplicationZone/ApplicationBook)
                // which were parsed from Remarks. Fall back to what the inspector submitted.
                int zone = record.ApplicationZone ?? record.Zone ?? 0,
                    book = record.ApplicationBook ?? record.Book ?? 0;
                var zoneBook = zone > 0 && book > 0
                    ? await _context.ZoneBooks.FirstOrDefaultAsync(x => x.Zone == zone && x.Book == book)
                    : null;

                if (zoneBook != null)
                {
                    var classification = application.ConnectionType.ToLowerInvariant() switch
                    {
                        "commercial"  => (int)AccountClassification.Commercial,
                        "commerciala" => (int)AccountClassification.CommercialA,
                        "commercialb" => (int)AccountClassification.CommercialB,
                        "commercialc" => (int)AccountClassification.CommercialC,
                        "government"  => (int)AccountClassification.Government,
                        "industrial"  => (int)AccountClassification.Industrial,
                        "wholesale"   => (int)AccountClassification.Wholesale,
                        "bulk"        => (int)AccountClassification.Bulk,
                        _             => (int)AccountClassification.Residential
                    };

                    // Sequence is per zone/book (series resets per book).
                    // Format: ZZ-BB-SSSS  e.g. 01-05-0042 = Zone 1, Book 5, Series 42
                    var maxSeq = await _context.Accounts
                        .Where(x => x.ZoneBookId == (int)zoneBook.Id)
                        .MaxAsync(x => (int?)x.Sequence) ?? 0;

                    var fullName = application.FullName.Trim();
                    string firstName, middleName, lastName;
                    if (fullName.Contains(','))
                    {
                        var p = fullName.Split(',', 2);
                        lastName = p[0].Trim();
                        var g = p[1].Trim().Split(' ', 2);
                        firstName = g[0].Trim(); middleName = g.Length > 1 ? g[1].Trim() : string.Empty;
                    }
                    else
                    {
                        var p = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        firstName = p.Length > 0 ? p[0] : fullName;
                        middleName = p.Length > 2 ? string.Join(" ", p[1..^1]) : string.Empty;
                        lastName = p.Length > 1 ? p[^1] : string.Empty;
                    }

                    var now2 = DateTime.Now;
                    var accountNumber = $"{zone:D2}-{book:D2}{(maxSeq + 1):D4}";

                    var customer = new Entities.Customer
                    {
                        Firstname = firstName, Middlename = middleName, Lastname = lastName,
                        PhoneNumber = (application.ContactNumber ?? string.Empty)[..Math.Min(11, (application.ContactNumber ?? string.Empty).Length)],
                        Email = application.Email ?? string.Empty, IsActive = true,
                        DateCreated = now2, DateUpdated = now2, CreatedBy = userId, Updatedby = userId
                    };
                    _context.Customers.Add(customer);
                    await _context.SaveChangesAsync();

                    var account = new Entities.Account
                    {
                        CustomerId = customer.Id, AccountNumber = accountNumber,
                        ZoneBookId = (int)zoneBook.Id, Classification = classification,
                        MeterSize = record.MeterSize > 0 ? record.MeterSize : 0.5m,
                        MeterNumber = record.MeterNumber ?? string.Empty,
                        UnitNumber  = application.UnitNumber ?? string.Empty,
                        Building    = application.Building   ?? string.Empty,
                        HouseNumber = application.HouseNumber ?? string.Empty,
                        Street      = application.Street     ?? application.Address ?? string.Empty,
                        Barangay    = application.Barangay   ?? string.Empty,
                        IsCurrentAddress = true, Sequence = maxSeq + 1,
                        Status = (int)AccountStatus.Pending,
                        DateCreated = now2, DateUpdated = now2, CreatedBy = userId, UpdatedBy = userId
                    };
                    _context.Accounts.Add(account);
                    await _context.SaveChangesAsync();

                    application.AccountNumber = accountNumber;
                    application.CustomerId = customer.Id;
                    application.AccountId = account.Id;
                    record.AccountNumber = accountNumber;

                    // ── Auto-charge Registration Fee if the inspector checked the box ──
                    if (record.ChargeNscFee)
                    {
                        // Find the Registration Fee charge type
                        var regFeeType = await _context.OtherFeeTypes
                            .FirstOrDefaultAsync(x => x.Name.ToLower().Contains("registration fee"));

                        if (regFeeType != null)
                        {
                            // Create the initial billing stub for this new account
                            var billingRef = Guid.NewGuid().ToString("N")[..12].ToUpper();
                            var billingStub = new Entities.Billing
                            {
                                AccountId          = account.Id,
                                BillingReferenceId = billingRef,
                                BillingPeriod      = now2,
                                TotalBillAmount    = 0,
                                RemainingAmount    = 0,
                                PaymentStatus      = (int)PaymentStatus.Draft,
                                CreatedBy          = userId,
                                DateCreated        = now2,
                                UpdatedBy          = userId,
                                DateUpdated        = now2
                            };
                            _context.Billings.Add(billingStub);
                            await _context.SaveChangesAsync();

                            // Attach the Registration Fee charge
                            _context.OtherCharges.Add(new Entities.OtherCharge
                            {
                                BillingReferenceId = billingRef,
                                Type               = (int)regFeeType.Id,
                                Amount             = regFeeType.DefaultValue > 0
                                                        ? regFeeType.DefaultValue
                                                        : 0,
                                PayableIn          = "Cash",
                                Particulars        = "New service connection — registration fee",
                                PaymentStatus      = (int)PaymentStatus.Unpaid,
                                IsActive           = true,
                                CreatedBy          = userId,
                                DateCreated        = now2,
                                UpdatedBy          = userId,
                                DateUpdated        = now2
                            });
                            await _context.SaveChangesAsync();

                            // The fee remains a draft until Charging explicitly
                            // posts it to Collection. Posting performs the rollup.
                        }
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(application.AccountNumber))
            {
                // Already activated — echo back the number so the UI can display it
                record.AccountNumber = application.AccountNumber;
            }

            // An approved inspection only needs charging/payment when the NSC fee
            // was requested. Otherwise it can proceed directly to installation.
            application.Stage = isApproval ? (record.ChargeNscFee ? "charging" : "installation")
                              : isDisapproved ? "rejected"
                              : "inspection";
            await _context.SaveChangesAsync();
            record.ReportId = application.Id;
            return Ok(record);
        }
        if (record.RequestId <= 0) return BadRequest("Service application is required.");
        if (!await _context.Requests.AnyAsync(x => x.Id == record.RequestId)) return NotFound("Service application was not found.");
        if (!record.DateInspected.HasValue) return BadRequest("Date inspected is required.");
        if (!record.InspectedByUserId.HasValue) return BadRequest("Inspector is required.");

        record.InspectedByName = await _context.Users.Where(x => x.Id == record.InspectedByUserId)
            .Select(x => x.Name ?? x.Email ?? string.Empty).FirstOrDefaultAsync() ?? string.Empty;
        record.ApprovedByName = record.ApprovedByUserId.HasValue
            ? await _context.Users.Where(x => x.Id == record.ApprovedByUserId)
                .Select(x => x.Name ?? x.Email ?? string.Empty).FirstOrDefaultAsync() ?? string.Empty
            : string.Empty;
        record.Inspectors = new();
        record.Approvers = new();
        record.Zones = new();

        var details = JsonSerializer.Serialize(record);
        var report = await _context.InspectionReports
            .Where(x => x.RequestId == record.RequestId).OrderByDescending(x => x.Id).FirstOrDefaultAsync();
        if (report == null)
        {
            report = new Entities.InspectionReport
            {
                RequestId = record.RequestId,
                InspectedByUserId = record.InspectedByUserId.Value,
                Details = details,
                CreatedBy = userId,
                DateCreated = DateTime.Now
            };
            _context.InspectionReports.Add(report);
        }
        else
        {
            report.InspectedByUserId = record.InspectedByUserId.Value;
            report.Details = details;
            report.UpdatedBy = userId;
            report.DateUpdated = DateTime.Now;
        }
        await _context.SaveChangesAsync();
        record.ReportId = report.Id;
        return Ok(record);
    }

    [HttpGet("ConnectOrder/{requestId:long}")]
    public async Task<ActionResult<ConnectOrderRecord>> ConnectOrder(long requestId)
    {
        var application = await _context.WaterConnectionApplications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == requestId);
        if (application != null)
        {
            ConnectOrderRecord applicationRecord;
            try { applicationRecord = JsonSerializer.Deserialize<ConnectOrderRecord>(application.ConnectData ?? "{}", new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new(); }
            catch (JsonException) { applicationRecord = new(); }
            ServiceInspectionRecord? applicationInspection = null;
            try { applicationInspection = JsonSerializer.Deserialize<ServiceInspectionRecord>(application.InspectionData ?? "{}", new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch (JsonException) { }
            var activeUsers = await _context.Users.AsNoTracking().Where(x => x.IsActive == true).OrderBy(x => x.Name)
                .Select(x => new { x.Id, Name = x.Name ?? x.Email ?? "User", x.Role }).ToListAsync();
            var processor = application.AssignedTo.HasValue ? activeUsers.FirstOrDefault(x => x.Id == application.AssignedTo && x.Role == (int)UserRole.CustomerRepresentative) : null;
            applicationRecord.RecommendationId = application.Id; applicationRecord.RequestId = application.Id;
            applicationRecord.ApplicationNumber = application.TrackingNumber; applicationRecord.ApplicantName = application.FullName;
            applicationRecord.DateApplied = application.CreatedAt ?? DateTime.MinValue;
            applicationRecord.Address = string.Join(" ", new[] { application.Address, application.Barangay }.Where(x => !string.IsNullOrWhiteSpace(x)));
            applicationRecord.Classification = application.ConnectionType; applicationRecord.ProcessedByName = processor?.Name ?? string.Empty;
            applicationRecord.InspectedByName = applicationInspection?.InspectedByName ?? string.Empty;
            applicationRecord.InspectionApprovedByName = applicationInspection?.ApprovedByName ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(application.PaymentData))
            {
                try { using var document = JsonDocument.Parse(application.PaymentData); var root = document.RootElement;
                    if (root.TryGetProperty("reference", out var reference)) applicationRecord.InvoiceNumber = reference.GetString() ?? string.Empty;
                    if (root.TryGetProperty("date", out var date) && date.TryGetDateTime(out var invoiceDate)) applicationRecord.InvoiceDate = invoiceDate; }
                catch (JsonException) { }
            }
            applicationRecord.Recommenders = activeUsers.Where(x => x.Role is (int)UserRole.Engineer or (int)UserRole.CustomerRepresentative or (int)UserRole.CustomerServiceOic or (int)UserRole.SuperAdmin)
                .Select(x => new ServiceInspectionOption { Value = x.Id, Label = x.Name }).ToList();
            applicationRecord.Approvers = activeUsers.Where(x => x.Role is (int)UserRole.CustomerServiceOic or (int)UserRole.SuperAdmin)
                .Select(x => new ServiceInspectionOption { Value = x.Id, Label = x.Name }).ToList();
            return Ok(applicationRecord);
        }
        var source = await (from request in _context.Requests.AsNoTracking()
                            join account in _context.Accounts.AsNoTracking() on request.AccountId equals account.Id
                            join customer in _context.Customers.AsNoTracking() on request.CustomerId equals customer.Id
                            where request.Id == requestId
                            select new { request, account, customer }).FirstOrDefaultAsync();
        if (source == null) return NotFound("Service application was not found.");

        var recommendation = await _context.Recommendations.AsNoTracking()
            .Where(x => x.RequestId == requestId).OrderByDescending(x => x.Id).FirstOrDefaultAsync();
        ConnectOrderRecord record;
        try
        {
            record = recommendation == null
                ? new ConnectOrderRecord()
                : JsonSerializer.Deserialize<ConnectOrderRecord>(recommendation.Details,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new ConnectOrderRecord();
        }
        catch (JsonException) { record = new ConnectOrderRecord(); }

        var inspection = await _context.InspectionReports.AsNoTracking()
            .Where(x => x.RequestId == requestId).OrderByDescending(x => x.Id).FirstOrDefaultAsync();
        ServiceInspectionRecord? inspectionDetails = null;
        if (inspection != null)
        {
            try { inspectionDetails = JsonSerializer.Deserialize<ServiceInspectionRecord>(inspection.Details, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch (JsonException) { }
        }

        var billingReferences = from job in _context.JobOrders.AsNoTracking()
                                join billing in _context.Billings.AsNoTracking() on (long)job.Id equals billing.JobOrderId
                                where job.RequestId == requestId
                                select billing.BillingReferenceId;
        var payment = await _context.Payments.AsNoTracking()
            .Where(x => x.Success && billingReferences.Contains(x.BillingReference))
            .OrderByDescending(x => x.DateCreated)
            .Select(x => new { InvoiceNumber = x.PaymentReference, InvoiceDate = (DateTime?)x.DateCreated })
            .FirstOrDefaultAsync();
        var jobProcessorId = await _context.JobOrders.AsNoTracking().Where(x => x.RequestId == requestId)
            .OrderByDescending(x => x.DateUpdated).ThenByDescending(x => x.DateCreated)
            .Select(x => (long?)(x.UpdatedBy != 0 ? x.UpdatedBy : x.CreatedBy)).FirstOrDefaultAsync();
        var processorIds = new[] { jobProcessorId ?? 0, source.request.UpdatedBy, source.request.CreatedBy }
            .Where(x => x > 0).Distinct().ToList();
        var customerRepresentatives = await _context.Users.AsNoTracking()
            .Where(x => processorIds.Contains(x.Id) && x.Role == (int)UserRole.CustomerRepresentative)
            .Select(x => new { x.Id, Name = x.Name ?? x.Email ?? string.Empty }).ToListAsync();
        var processedBy = processorIds.Select(id => customerRepresentatives.FirstOrDefault(x => x.Id == id)?.Name)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? string.Empty;
        var users = await _context.Users.AsNoTracking().Where(x => x.IsActive == true).OrderBy(x => x.Name)
            .Select(x => new { x.Id, Name = x.Name ?? x.Email ?? "User", x.Role }).ToListAsync();

        record.RecommendationId = recommendation?.Id ?? 0;
        record.RequestId = requestId;
        record.ApplicationNumber = source.request.ControlNumber;
        record.ApplicantName = $"{source.customer.Lastname}, {source.customer.Firstname} {source.customer.Middlename}".Trim();
        record.DateApplied = source.request.DateCreated;
        record.Address = string.Join(" ", new[] { source.account.UnitNumber, source.account.Building,
            source.account.HouseNumber, source.account.Street, source.account.Barangay }.Where(x => !string.IsNullOrWhiteSpace(x)));
        record.Classification = Enum.IsDefined(typeof(AccountClassification), source.account.Classification)
            ? ((AccountClassification)source.account.Classification).ToString() : "Unclassified";
        record.ProcessedByName = processedBy;
        record.InspectedByName = inspectionDetails?.InspectedByName ?? string.Empty;
        record.InspectionApprovedByName = inspectionDetails?.ApprovedByName ?? string.Empty;
        record.InvoiceNumber = payment?.InvoiceNumber ?? string.Empty;
        record.InvoiceDate = payment?.InvoiceDate;
        record.Recommenders = users.Where(x => x.Role is (int)UserRole.Engineer or (int)UserRole.CustomerRepresentative or (int)UserRole.CustomerServiceOic or (int)UserRole.SuperAdmin)
            .Select(x => new ServiceInspectionOption { Value = x.Id, Label = x.Name }).ToList();
        record.Approvers = users.Where(x => x.Role is (int)UserRole.CustomerServiceOic or (int)UserRole.SuperAdmin)
            .Select(x => new ServiceInspectionOption { Value = x.Id, Label = x.Name }).ToList();
        return Ok(record);
    }

    [HttpPost("SaveConnectOrder/{userId:long}")]
    public async Task<ActionResult<ConnectOrderRecord>> SaveConnectOrder(long userId, [FromBody] ConnectOrderRecord record)
    {
        var application = await _context.WaterConnectionApplications.FirstOrDefaultAsync(x => x.Id == record.RequestId);
        if (application != null)
        {
            record.RecommendedApprovalByName = record.RecommendedApprovalByUserId.HasValue ? await _context.Users.Where(x => x.Id == record.RecommendedApprovalByUserId).Select(x => x.Name ?? x.Email ?? string.Empty).FirstOrDefaultAsync() ?? string.Empty : string.Empty;
            record.ApprovedByName = record.ApprovedByUserId.HasValue ? await _context.Users.Where(x => x.Id == record.ApprovedByUserId).Select(x => x.Name ?? x.Email ?? string.Empty).FirstOrDefaultAsync() ?? string.Empty : string.Empty;
            record.Recommenders = new(); record.Approvers = new();
            application.ConnectData = JsonSerializer.Serialize(record);
            // Only advance to tapping once the connect order is fully approved
            if (record.ApprovedByUserId.HasValue && record.DateApproved.HasValue)
                application.Stage = "tapping";
            application.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync(); record.RecommendationId = application.Id; return Ok(record);
        }
        if (record.RequestId <= 0) return BadRequest("Service application is required.");
        if (!await _context.Requests.AnyAsync(x => x.Id == record.RequestId)) return NotFound("Service application was not found.");
        record.RecommendedApprovalByName = record.RecommendedApprovalByUserId.HasValue
            ? await _context.Users.Where(x => x.Id == record.RecommendedApprovalByUserId)
                .Select(x => x.Name ?? x.Email ?? string.Empty).FirstOrDefaultAsync() ?? string.Empty : string.Empty;
        record.ApprovedByName = record.ApprovedByUserId.HasValue
            ? await _context.Users.Where(x => x.Id == record.ApprovedByUserId)
                .Select(x => x.Name ?? x.Email ?? string.Empty).FirstOrDefaultAsync() ?? string.Empty : string.Empty;
        record.Recommenders = new();
        record.Approvers = new();
        var details = JsonSerializer.Serialize(record);
        var recommendation = await _context.Recommendations
            .Where(x => x.RequestId == record.RequestId).OrderByDescending(x => x.Id).FirstOrDefaultAsync();
        if (recommendation == null)
        {
            recommendation = new Entities.Recommendation
            {
                RequestId = record.RequestId, Details = details, CreatedBy = userId, DateCreated = DateTime.Now
            };
            _context.Recommendations.Add(recommendation);
        }
        else
        {
            recommendation.Details = details;
            recommendation.UpdatedBy = userId;
            recommendation.DateUpdated = DateTime.Now;
        }
        await _context.SaveChangesAsync();
        record.RecommendationId = recommendation.Id;
        return Ok(record);
    }

    // ── Payment detail ──────────────────────────────────────────────────────
    [HttpGet("ApplicationPayment/{applicationId:long}")]
    public async Task<ActionResult<ApplicationPaymentRecord>> ApplicationPayment(long applicationId)
    {
        await ReconcilePaidApplicationPayments();

        var app = await _context.WaterConnectionApplications.AsNoTracking()
            .Where(x => x.Id == applicationId)
            .FirstOrDefaultAsync();
        if (app == null) return NotFound("Application not found.");

        var record = new ApplicationPaymentRecord
        {
            ApplicationId  = app.Id,
            TrackingNumber = app.TrackingNumber,
            FullName       = app.FullName,
            Address        = app.Address,
            Barangay       = app.Barangay,
            ContactNumber  = app.ContactNumber,
            ConnectionType = app.ConnectionType,
            Source         = app.Source,
            Stage          = app.Stage,
            CreatedAt      = app.CreatedAt,
            Remarks        = app.Remarks
        };

        // Parse charging_data for fee reference / amount
        if (!string.IsNullOrWhiteSpace(app.ChargingData))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(app.ChargingData);
                var root = doc.RootElement;
                if (root.TryGetProperty("reference", out var r)) record.ChargeReference = r.GetString();
                if (root.TryGetProperty("amount", out var a) && a.TryGetDecimal(out var amt)) record.ChargeAmount = amt;
            }
            catch (System.Text.Json.JsonException) { }
        }

        // Parse payment_data for receipt info
        if (!string.IsNullOrWhiteSpace(app.PaymentData))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(app.PaymentData);
                var root = doc.RootElement;
                if (root.TryGetProperty("reference", out var r))  record.PaymentReference = r.GetString();
                if (root.TryGetProperty("method", out var m))     record.PaymentMethod    = m.GetString();
                if (root.TryGetProperty("status", out var s))     record.PaymentStatus    = s.GetString();
                if (root.TryGetProperty("date", out var d) && d.TryGetDateTime(out var dt)) record.PaymentDate = dt;
            }
            catch (System.Text.Json.JsonException) { }
        }

        return Ok(record);
    }

    // ── Ticket (full application detail) ────────────────────────────────────
    /// <summary>
    /// Keeps older applications linked to their generated account number. Some
    /// records have AccountId populated but predate writing AccountNumber back
    /// to the application itself.
    /// </summary>
    private async Task BackfillApplicationAccountNumbers()
    {
        var applications = await _context.WaterConnectionApplications
            .Where(x => x.AccountId.HasValue
                && (x.AccountNumber == null || x.AccountNumber == string.Empty))
            .ToListAsync();
        if (applications.Count == 0) return;

        var accountIds = applications.Select(x => x.AccountId!.Value).Distinct().ToList();
        var accountNumbers = await _context.Accounts.AsNoTracking()
            .Where(x => accountIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.AccountNumber);

        var changed = false;
        foreach (var application in applications)
        {
            if (!accountNumbers.TryGetValue(application.AccountId!.Value, out var accountNumber)
                || string.IsNullOrWhiteSpace(accountNumber))
                continue;

            application.AccountNumber = accountNumber;
            application.UpdatedAt = DateTime.Now;
            changed = true;
        }

        if (changed) await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Repairs applications paid through the standalone Other Charges cashier
    /// modal before that workflow wrote payment_data. The successful payment is
    /// the source of truth; only fully paid connection-fee billings are advanced.
    /// </summary>
    private async Task ReconcilePaidApplicationPayments()
    {
        var applications = await _context.WaterConnectionApplications
            .Where(x => x.Stage == "payment" && x.AccountId.HasValue
                && x.ChargingData != null && x.ChargingData != string.Empty)
            .ToListAsync();
        if (applications.Count == 0) return;

        var changed = false;
        foreach (var application in applications)
        {
            string? billingReference = null;
            try
            {
                using var charging = JsonDocument.Parse(application.ChargingData!);
                if (charging.RootElement.TryGetProperty("reference", out var referenceElement))
                    billingReference = referenceElement.GetString();
            }
            catch (JsonException) { }

            if (string.IsNullOrWhiteSpace(billingReference)) continue;

            var charges = await _context.OtherCharges
                .Where(x => x.BillingReferenceId == billingReference && x.IsActive)
                .ToListAsync();
            if (charges.Count == 0
                || charges.Any(x => x.PaymentStatus != (int)PaymentStatus.Paid))
                continue;

            var payment = await _context.Payments.AsNoTracking()
                .Where(x => x.BillingReference == billingReference && x.Success)
                .OrderByDescending(x => x.DateCreated)
                .FirstOrDefaultAsync();
            if (payment == null) continue;

            var billing = await _context.Billings
                .FirstOrDefaultAsync(x => x.BillingReferenceId == billingReference);
            if (billing == null) continue;

            var previousOtherCharges = billing.OtherCharges;
            billing.OtherCharges = 0m;
            billing.TotalBillAmount = Math.Max(0m, billing.TotalBillAmount - previousOtherCharges);
            billing.RemainingAmount = Math.Max(0m, billing.RemainingAmount - previousOtherCharges);
            changed = true;

            var hasOutstandingPenalty = await _context.Penalties.AnyAsync(x =>
                x.BillingReferenceId == billingReference
                && (x.PaymentStatus == (int)PaymentStatus.Unpaid
                 || x.PaymentStatus == (int)PaymentStatus.Overdue));
            if (billing.RemainingAmount > 0m || hasOutstandingPenalty) continue;

            billing.PaymentStatus = (int)PaymentStatus.Paid;
            billing.PaymentTransactionId = payment.PaymentReference;
            billing.UpdatedBy = payment.CreatedBy;
            billing.DateUpdated = payment.DateCreated;

            var method = Enum.IsDefined(typeof(PaymentMethod), payment.Method)
                ? ((PaymentMethod)payment.Method).ToString()
                : "Unknown";
            application.PaymentData = JsonSerializer.Serialize(new
            {
                reference = payment.PaymentReference,
                method,
                date = payment.DateCreated,
                status = "Paid"
            });
            application.Stage = "installation";
            application.UpdatedAt = DateTime.Now;
        }

        if (changed) await _context.SaveChangesAsync();
    }

    [HttpGet("ApplicationTicket/{applicationId:long}")]
    public async Task<ActionResult<ApplicationTicketRecord>> ApplicationTicket(long applicationId)
    {
        var app = await _context.WaterConnectionApplications.AsNoTracking()
            .Where(x => x.Id == applicationId)
            .FirstOrDefaultAsync();
        if (app == null) return NotFound("Application not found.");

        return Ok(new ApplicationTicketRecord
        {
            ApplicationId    = app.Id,
            TrackingNumber   = app.TrackingNumber,
            FullName         = app.FullName,
            Address          = app.Address,
            Barangay         = app.Barangay,
            ContactNumber    = app.ContactNumber,
            Email            = app.Email,
            ConnectionType   = app.ConnectionType,
            Source           = app.Source,
            Stage            = app.Stage,
            Remarks          = app.Remarks,
            CreatedAt        = app.CreatedAt,
            UpdatedAt        = app.UpdatedAt,
            InspectionData   = app.InspectionData,
            ChargingData     = app.ChargingData,
            PaymentData      = app.PaymentData,
            InstallationData = app.InstallationData,
            ConnectData      = app.ConnectData,
            TappingData      = app.TappingData,
            DoneData         = app.DoneData
        });
    }

    [HttpGet("WorkOrder/{workOrderType}/{requestId:long}")]
    public async Task<ActionResult<ServiceWorkOrderRecord>> WorkOrder(string workOrderType, long requestId)
    {
        workOrderType = workOrderType.Trim().ToLowerInvariant();
        if (workOrderType is not ("installation" or "tapping")) return BadRequest("Unknown work order type.");
        await BackfillApplicationAccountNumbers();
        var application = await _context.WaterConnectionApplications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == requestId);
        if (application != null)
        {
            var linkedAccount = application.AccountId.HasValue
                ? await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == application.AccountId.Value)
                : null;
            var json = workOrderType == "installation" ? application.InstallationData : application.TappingData;
            ServiceWorkOrderRecord applicationRecord;
            try { applicationRecord = JsonSerializer.Deserialize<ServiceWorkOrderRecord>(json ?? "{}", new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new(); }
            catch (JsonException) { applicationRecord = new(); }
            applicationRecord.FindingId = application.Id; applicationRecord.RequestId = application.Id; applicationRecord.JobOrderId = application.Id;
            applicationRecord.WorkOrderType = workOrderType; applicationRecord.ApplicationNumber = application.TrackingNumber;
            applicationRecord.JobOrderNumber = $"{(workOrderType == "installation" ? "INS" : "TAP")}-{application.TrackingNumber}";
            applicationRecord.ApplicantName = application.FullName; applicationRecord.Address = string.Join(" ", new[] { application.Address, application.Barangay }.Where(x => !string.IsNullOrWhiteSpace(x)));
            applicationRecord.ServiceName = application.ConnectionType + " connection";
            applicationRecord.AccountNumber = !string.IsNullOrWhiteSpace(application.AccountNumber)
                ? application.AccountNumber
                : linkedAccount?.AccountNumber ?? string.Empty;
            if (string.IsNullOrWhiteSpace(applicationRecord.MeterNumber))
                applicationRecord.MeterNumber = linkedAccount?.MeterNumber ?? string.Empty;
            if (string.IsNullOrWhiteSpace(applicationRecord.PipeSize) && linkedAccount?.MeterSize > 0)
                applicationRecord.PipeSize = linkedAccount.MeterSize.ToString("0.##");
            applicationRecord.Personnel = await _context.Users.AsNoTracking().Where(x => x.IsActive == true && (x.Role == (int)UserRole.Engineer || x.Role == (int)UserRole.SuperAdmin))
                .OrderBy(x => x.Name).Select(x => new ServiceInspectionOption { Value = x.Id, Label = x.Name ?? x.Email ?? "User" }).ToListAsync();
            return Ok(applicationRecord);
        }
        var source = await (from request in _context.Requests.AsNoTracking()
                            join account in _context.Accounts.AsNoTracking() on request.AccountId equals account.Id
                            join customer in _context.Customers.AsNoTracking() on request.CustomerId equals customer.Id
                            where request.Id == requestId
                            select new { request, account, customer }).FirstOrDefaultAsync();
        if (source == null) return NotFound("Service application was not found.");
        var jobs = await (from job in _context.JobOrders.AsNoTracking()
                          join detail in _context.RequestDetails.AsNoTracking() on job.RequestDetailId equals detail.Id
                          join type in _context.InspectionTypes.AsNoTracking() on detail.RequestTypeId equals type.Id
                          where job.RequestId == requestId
                          orderby job.DateUpdated descending, job.DateCreated descending
                          select new { job, ServiceName = type.Name }).ToListAsync();
        var selected = workOrderType == "tapping"
            ? jobs.FirstOrDefault(x => x.ServiceName.Contains("tap", StringComparison.OrdinalIgnoreCase))
            : jobs.FirstOrDefault(x => x.job.Status == (int)JobOrderStatus.Installation) ?? jobs.FirstOrDefault();
        if (selected == null) return NotFound("No job order was found for this application.");

        ServiceWorkOrderRecord? stored = null;
        long findingId = 0;
        var findings = await _context.Findings.AsNoTracking().Where(x => x.JobOrderId == selected.job.Id)
            .OrderByDescending(x => x.Id).ToListAsync();
        foreach (var finding in findings)
        {
            try
            {
                var candidate = JsonSerializer.Deserialize<ServiceWorkOrderRecord>(finding.Detail, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (candidate?.WorkOrderType.Equals(workOrderType, StringComparison.OrdinalIgnoreCase) == true)
                { stored = candidate; findingId = finding.Id; break; }
            }
            catch (JsonException) { }
        }
        var record = stored ?? new ServiceWorkOrderRecord();
        record.FindingId = findingId;
        record.RequestId = requestId;
        record.JobOrderId = selected.job.Id;
        record.WorkOrderType = workOrderType;
        record.ApplicationNumber = source.request.ControlNumber;
        record.JobOrderNumber = selected.job.JobOrderNumber;
        record.ApplicantName = $"{source.customer.Lastname}, {source.customer.Firstname} {source.customer.Middlename}".Trim();
        record.AccountNumber = source.account.AccountNumber;
        record.Address = string.Join(" ", new[] { source.account.UnitNumber, source.account.Building,
            source.account.HouseNumber, source.account.Street, source.account.Barangay }.Where(x => !string.IsNullOrWhiteSpace(x)));
        record.ServiceName = selected.ServiceName;
        record.MeterNumber = source.account.MeterNumber;
        record.PipeSize = source.account.MeterSize > 0 ? source.account.MeterSize.ToString("0.##") : record.PipeSize;
        record.Personnel = await _context.Users.AsNoTracking()
            .Where(x => x.IsActive == true && (x.Role == (int)UserRole.Engineer || x.Role == (int)UserRole.SuperAdmin))
            .OrderBy(x => x.Name).Select(x => new ServiceInspectionOption { Value = x.Id, Label = x.Name ?? x.Email ?? "User" }).ToListAsync();
        var releasedMaterials = await (from material in _context.Materials.AsNoTracking()
                                       join inventory in _context.Inventories.AsNoTracking() on material.InventoryId equals inventory.Id
                                       where material.JobOrderId == selected.job.Id
                                       select new ServiceWorkOrderMaterial { Description = inventory.Name + " (" + inventory.UOM + ")", Quantity = material.RequestedQuantity }).ToListAsync();
        if (releasedMaterials.Count > 0) record.Materials = releasedMaterials;
        return Ok(record);
    }

    [HttpGet("HouseTapping/{requestId:long}")]
    public async Task<ActionResult<HouseTappingRecord>> HouseTapping(long requestId)
    {
        var application = await _context.WaterConnectionApplications.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == requestId);
        if (application == null) return NotFound("Tapping application was not found.");
        if (application.AccountId is null or <= 0) return BadRequest("The tapping application has no linked account.");

        var account = await _context.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == application.AccountId.Value);
        if (account == null) return NotFound("The linked account was not found.");

        HouseTappingRecord record;
        var hasStoredRecord = !string.IsNullOrWhiteSpace(application.TappingData);
        try
        {
            record = JsonSerializer.Deserialize<HouseTappingRecord>(application.TappingData ?? "{}",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch (JsonException) { record = new(); }

        ServiceInspectionRecord? inspection = null;
        try
        {
            inspection = JsonSerializer.Deserialize<ServiceInspectionRecord>(application.InspectionData ?? "{}",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException) { }

        var zoneBook = await _context.ZoneBooks.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == account.ZoneBookId);
        record.RequestId = application.Id;
        record.ApplicationNumber = application.TrackingNumber;
        record.ApplicantName = application.FullName;
        record.DateApplied = application.CreatedAt;
        record.Address = string.Join(" ", new[] { application.Address, application.Barangay }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        record.Classification = Enum.IsDefined(typeof(AccountClassification), account.Classification)
            ? ((AccountClassification)account.Classification).ToString()
            : application.ConnectionType;
        record.InspectedByName = inspection?.InspectedByName ?? string.Empty;
        record.DateInspected = inspection?.DateInspected;
        record.Zone ??= zoneBook?.Zone;
        record.Book ??= zoneBook?.Book;
        if (!hasStoredRecord)
        {
            record.SerialNumber = account.MeterNumber;
            record.MeterSize = account.MeterSize > 0 ? account.MeterSize : 0.5m;
            record.DateAccomplished = DateTime.Now;
        }

        record.ZoneBooks = await _context.ZoneBooks.AsNoTracking()
            .OrderBy(x => x.Zone).ThenBy(x => x.Book)
            .Select(x => new ServiceInspectionZone { Zone = x.Zone, Book = x.Book })
            .ToListAsync();
        record.MeterBrands = await _context.Inventories.AsNoTracking()
            .Where(x => x.IsActive && x.Name.ToLower().Contains("meter"))
            .OrderBy(x => x.Name).Select(x => x.Name).Distinct().ToListAsync();
        if (!string.IsNullOrWhiteSpace(record.MeterBrand)
            && !record.MeterBrands.Contains(record.MeterBrand, StringComparer.OrdinalIgnoreCase))
            record.MeterBrands.Insert(0, record.MeterBrand);
        if (record.MeterBrands.Count == 0) record.MeterBrands.Add("Other");
        record.Personnel = await _context.Users.AsNoTracking().Where(x => x.IsActive == true)
            .OrderBy(x => x.Name)
            .Select(x => new ServiceInspectionOption { Value = x.Id, Label = x.Name ?? x.Email ?? "User" })
            .ToListAsync();
        return Ok(record);
    }

    [HttpPost("SaveHouseTapping/{userId:long}")]
    public async Task<ActionResult<HouseTappingRecord>> SaveHouseTapping(
        long userId, [FromBody] HouseTappingRecord record)
    {
        if (userId <= 0) return Unauthorized();
        var application = await _context.WaterConnectionApplications
            .FirstOrDefaultAsync(x => x.Id == record.RequestId);
        if (application == null) return NotFound("Tapping application was not found.");
        if (!application.Stage.Equals("tapping", StringComparison.OrdinalIgnoreCase))
            return Conflict("This application is no longer waiting for tapping.");
        if (application.AccountId is null or <= 0) return BadRequest("The tapping application has no linked account.");
        if (record.Zone is null or <= 0 || record.Book is null or <= 0) return BadRequest("Zone and book are required.");
        if (string.IsNullOrWhiteSpace(record.MeterBrand)) return BadRequest("Meter brand is required.");
        if (record.MeterSize <= 0) return BadRequest("Meter size is required.");
        if (string.IsNullOrWhiteSpace(record.SerialNumber)) return BadRequest("Serial number is required.");
        if (record.InitialReading < 0) return BadRequest("Initial reading cannot be negative.");
        if (record.AccomplishedByUserId is null or <= 0) return BadRequest("Accomplished by is required.");
        if (!record.DateAccomplished.HasValue) return BadRequest("Date accomplished is required.");

        var zoneBook = await _context.ZoneBooks
            .FirstOrDefaultAsync(x => x.Zone == record.Zone.Value && x.Book == record.Book.Value);
        if (zoneBook == null) return BadRequest($"Zone {record.Zone} / Book {record.Book} does not exist.");
        var account = await _context.Accounts.FirstOrDefaultAsync(x => x.Id == application.AccountId.Value);
        if (account == null) return NotFound("The linked account was not found.");
        record.AccomplishedByName = await _context.Users.AsNoTracking()
            .Where(x => x.Id == record.AccomplishedByUserId.Value && x.IsActive == true)
            .Select(x => x.Name ?? x.Email ?? string.Empty).FirstOrDefaultAsync() ?? string.Empty;
        if (record.AccomplishedByName.Length == 0) return BadRequest("Select valid personnel for Accomplished By.");

        account.ZoneBookId = checked((int)zoneBook.Id);
        account.MeterNumber = record.SerialNumber.Trim();
        account.MeterSize = record.MeterSize;
        account.Status = (int)AccountStatus.Active;
        account.UpdatedBy = userId;
        account.DateUpdated = DateTime.Now;
        record.ApplicationNumber = application.TrackingNumber;
        record.ApplicantName = application.FullName;
        record.ZoneBooks = new(); record.MeterBrands = new(); record.Personnel = new();
        var completionData = JsonSerializer.Serialize(record);
        application.TappingData = completionData;
        application.DoneData = completionData;
        application.Stage = "done";
        application.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        await ReconcileCompletedServiceRequests();
        return Ok(record);
    }

    [HttpPost("SaveWorkOrder/{userId:long}")]
    public async Task<ActionResult<ServiceWorkOrderRecord>> SaveWorkOrder(long userId, [FromBody] ServiceWorkOrderRecord record)
    {
        var application = await _context.WaterConnectionApplications.FirstOrDefaultAsync(x => x.Id == record.RequestId);
        if (application != null)
        {
            if (!record.AssignedToUserId.HasValue) return BadRequest("Assigned personnel is required.");
            if (!record.ScheduledDate.HasValue) return BadRequest("Scheduled date is required.");
            record.AssignedToName = await _context.Users.Where(x => x.Id == record.AssignedToUserId).Select(x => x.Name ?? x.Email ?? string.Empty).FirstOrDefaultAsync() ?? string.Empty;
            record.Personnel = new();
            var json = JsonSerializer.Serialize(record);
            if (record.WorkOrderType == "installation")
            {
                application.InstallationData = json;
                // installation saved → move to connect for authorization
                if (record.Status == "Completed") application.Stage = "connect";
            }
            else if (record.WorkOrderType == "tapping")
            {
                application.TappingData = json;
                // Preserve the final tapping snapshot when legacy clients complete the workflow.
                if (record.Status == "Completed")
                {
                    application.DoneData = json;
                    application.Stage = "done";
                }
            }
            else return BadRequest("Unknown work order type.");
            application.UpdatedAt = DateTime.Now; await _context.SaveChangesAsync(); record.FindingId = application.Id; return Ok(record);
        }
        if (record.JobOrderId <= 0 || record.RequestId <= 0) return BadRequest("Job order is required.");
        if (record.WorkOrderType is not ("installation" or "tapping")) return BadRequest("Unknown work order type.");
        if (!record.AssignedToUserId.HasValue) return BadRequest("Assigned personnel is required.");
        if (!record.ScheduledDate.HasValue) return BadRequest("Scheduled date is required.");
        record.AssignedToName = await _context.Users.Where(x => x.Id == record.AssignedToUserId)
            .Select(x => x.Name ?? x.Email ?? string.Empty).FirstOrDefaultAsync() ?? string.Empty;
        record.Personnel = new();
        var details = JsonSerializer.Serialize(record);
        var finding = record.FindingId > 0 ? await _context.Findings.FirstOrDefaultAsync(x => x.Id == record.FindingId) : null;
        if (finding == null)
        {
            finding = new Entities.Finding { JobOrderId = record.JobOrderId, Detail = details, CreatedBy = userId, DateCreated = DateTime.Now };
            _context.Findings.Add(finding);
        }
        else
        {
            finding.Detail = details; finding.UpdatedBy = userId; finding.DateUpdated = DateTime.Now;
        }
        await _context.SaveChangesAsync();
        record.FindingId = finding.Id;
        return Ok(record);
    }

    // ── Activate Account ─────────────────────────────────────────────────────
    /// <summary>
    /// Creates a Customer + Account from a completed water_connection_applications record
    /// and writes the generated account number back to the application row.
    /// Must only be called once the application is at stage 'done' (or 'tapping' completed).
    /// </summary>
    [HttpPost("ActivateAccount/{userId:long}")]
    public async Task<ActionResult<ActivateAccountResult>> ActivateAccount(
        long userId, [FromBody] ActivateAccountRequest request)
    {
        if (userId <= 0) return Unauthorized();
        if (request.ApplicationId <= 0) return BadRequest("Application is required.");

        var app = await _context.WaterConnectionApplications
            .FirstOrDefaultAsync(x => x.Id == request.ApplicationId);
        if (app == null) return NotFound("Application not found.");

        // Guard: already activated
        if (!string.IsNullOrWhiteSpace(app.AccountNumber))
            return Ok(new ActivateAccountResult
            {
                ApplicationId = app.Id,
                AccountNumber = app.AccountNumber,
                CustomerId    = app.CustomerId ?? 0,
                AccountId     = app.AccountId  ?? 0,
                AlreadyActivated = true
            });

        // ── Parse Zone / Book from InspectionData ───────────────────────────
        int zone = request.Zone, book = request.Book;
        if (zone <= 0 || book <= 0)
        {
            if (!string.IsNullOrWhiteSpace(app.InspectionData))
            {
                try
                {
                    using var doc = JsonDocument.Parse(app.InspectionData);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("zone", out var z) && z.ValueKind != JsonValueKind.Null)
                        zone = z.GetInt32();
                    if (root.TryGetProperty("book", out var b) && b.ValueKind != JsonValueKind.Null)
                        book = b.GetInt32();
                }
                catch (JsonException) { }
            }
        }
        if (zone <= 0) return BadRequest("Zone is required. Set it in the inspection form or pass it explicitly.");
        if (book <= 0) return BadRequest("Book is required. Set it in the inspection form or pass it explicitly.");

        var zoneBook = await _context.ZoneBooks
            .FirstOrDefaultAsync(x => x.Zone == zone && x.Book == book);
        if (zoneBook == null) return BadRequest($"Zone {zone} / Book {book} does not exist.");

        // ── Determine classification ─────────────────────────────────────────
        var classification = app.ConnectionType.ToLowerInvariant() switch
        {
            "commercial"  => (int)AccountClassification.Commercial,
            "commerciala" => (int)AccountClassification.CommercialA,
            "commercialb" => (int)AccountClassification.CommercialB,
            "commercialc" => (int)AccountClassification.CommercialC,
            "government"  => (int)AccountClassification.Government,
            "industrial"  => (int)AccountClassification.Industrial,
            "wholesale"   => (int)AccountClassification.Wholesale,
            "bulk"        => (int)AccountClassification.Bulk,
            _             => (int)AccountClassification.Residential
        };

        // ── Next sequence number — per zone/book (series resets per book) ───
        // Format: ZZ-BBSSSS  e.g. 03-090637 = Zone 3, Book 09, Series 0637
        var maxSeq = await _context.Accounts
            .Where(x => x.ZoneBookId == (int)zoneBook.Id)
            .MaxAsync(x => (int?)x.Sequence) ?? 0;
        var sequence = maxSeq + 1;

        // ── Parse applicant name into First / Middle / Last ──────────────────
        // FullName is stored as-is from the application form.
        // Best-effort split: "Last, First Middle" OR "First Middle Last"
        string firstName, middleName, lastName;
        var fullName = app.FullName.Trim();
        if (fullName.Contains(','))
        {
            var parts = fullName.Split(',', 2);
            lastName  = parts[0].Trim();
            var given = parts[1].Trim().Split(' ', 2);
            firstName  = given[0].Trim();
            middleName = given.Length > 1 ? given[1].Trim() : string.Empty;
        }
        else
        {
            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            firstName  = parts.Length > 0 ? parts[0] : fullName;
            middleName = parts.Length > 2 ? string.Join(" ", parts[1..^1]) : string.Empty;
            lastName   = parts.Length > 1 ? parts[^1] : string.Empty;
        }

        // ── Generate account number ──────────────────────────────────────────
        // Format: ZZ-BBSSSS  e.g. 03-090637 = Zone 3, Book 09, Series 0637
        var now = DateTime.Now;
        var accountNumber = $"{zone:D2}-{book:D2}{sequence:D4}";

        await using var transaction = await _context.Database.BeginTransactionAsync();

        // 1. Create Customer
        var customer = new Entities.Customer
        {
            Firstname   = firstName,
            Middlename  = middleName,
            Lastname    = lastName,
            PhoneNumber = (app.ContactNumber ?? string.Empty)[..Math.Min(11, (app.ContactNumber ?? string.Empty).Length)],
            Email       = app.Email ?? string.Empty,
            IsActive    = true,
            DateCreated = now,
            DateUpdated = now,
            CreatedBy   = userId,
            Updatedby   = userId
        };
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        // 2. Create Account
        var account = new Entities.Account
        {
            CustomerId       = customer.Id,
            AccountNumber    = accountNumber,
            ZoneBookId       = (int)zoneBook.Id,
            Classification   = classification,
            MeterSize        = request.MeterSize > 0 ? request.MeterSize : 0.5m,
            MeterNumber      = request.MeterNumber ?? string.Empty,
            UnitNumber       = app.UnitNumber  ?? string.Empty,
            Building         = app.Building    ?? string.Empty,
            HouseNumber      = app.HouseNumber ?? string.Empty,
            Street           = !string.IsNullOrWhiteSpace(app.Street)
                                   ? app.Street
                                   : app.Address ?? string.Empty,
            Barangay         = app.Barangay    ?? string.Empty,
            IsCurrentAddress = true,
            Sequence         = sequence,
            Status           = (int)AccountStatus.Pending,
            DateCreated      = now,
            DateUpdated      = now,
            CreatedBy        = userId,
            UpdatedBy        = userId
        };
        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        // 3. Write account number back to the application
        app.AccountNumber = accountNumber;
        app.CustomerId    = customer.Id;
        app.AccountId     = account.Id;
        app.Stage         = "done";
        app.UpdatedAt     = now;
        await _context.SaveChangesAsync();

        await transaction.CommitAsync();

        return Ok(new ActivateAccountResult
        {
            ApplicationId    = app.Id,
            AccountNumber    = accountNumber,
            CustomerId       = customer.Id,
            AccountId        = account.Id,
            AlreadyActivated = false
        });
    }

    // ── Advance Stage ─────────────────────────────────────────────────────────
    /// <summary>
    /// Explicitly advance a water_connection_applications record to the next stage.
    /// Used by the New Application review modal to initiate inspection.
    /// Allowed transitions: application → inspection
    /// (all other stage transitions happen automatically inside their Save endpoints)
    /// </summary>
    [HttpPost("AdvanceStage/{userId:long}")]
    public async Task<ActionResult<AdvanceStageResult>> AdvanceStage(
        long userId, [FromBody] AdvanceStageRequest request)
    {
        if (userId <= 0) return Unauthorized();
        if (request.ApplicationId <= 0) return BadRequest("Application is required.");

        var app = await _context.WaterConnectionApplications
            .FirstOrDefaultAsync(x => x.Id == request.ApplicationId);
        if (app == null) return NotFound("Application not found.");

        // Only allow explicit manual advances for the intake → inspection step.
        // All subsequent steps advance automatically when the relevant form is saved.
        var allowed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["application"] = "inspection"
        };

        if (!allowed.TryGetValue(app.Stage, out var nextStage))
            return BadRequest($"Applications at stage '{app.Stage}' cannot be manually advanced. " +
                              "Complete the relevant form to move to the next stage.");

        app.Stage     = nextStage;
        app.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();

        return Ok(new AdvanceStageResult
        {
            ApplicationId = app.Id,
            PreviousStage = request.ExpectedCurrentStage ?? string.Empty,
            NewStage      = nextStage
        });
    }
}
