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

    [HttpGet("ServiceSummary")]
    public async Task<ActionResult<ServiceDashboard>> ServiceSummary()
    {
        var requestCounts = await _context.Requests.AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);

        var stageCounts = await _context.JobOrders.AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Select(j => j.RequestId).Distinct().Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);

        var summary = await (from detail in _context.RequestDetails.AsNoTracking()
                             join type in _context.InspectionTypes.AsNoTracking()
                                 on detail.RequestTypeId equals type.Id
                             group detail by type.Name into grouped
                             orderby grouped.Count() descending, grouped.Key
                             select new ServiceRequestSummary
                             {
                                 Request = grouped.Key,
                                 Count = grouped.Select(x => x.RequestId).Distinct().Count()
                             }).ToListAsync();

        var totalRequests = requestCounts.Values.Sum();
        return Ok(new ServiceDashboard
        {
            OnlineApplications = totalRequests,
            NewRequests = requestCounts.GetValueOrDefault((int)RequestStatus.Draft),
            Inspection = stageCounts.GetValueOrDefault((int)JobOrderStatus.Inspection),
            Charging = stageCounts.GetValueOrDefault((int)JobOrderStatus.Charging),
            Payment = stageCounts.GetValueOrDefault((int)JobOrderStatus.Payment),
            Installation = stageCounts.GetValueOrDefault((int)JobOrderStatus.Installation),
            Connect = summary.Where(x => x.Request.Contains("connect", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Count),
            Tapping = summary.Where(x => x.Request.Contains("tap", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Count),
            Tickets = totalRequests,
            RequestSummary = summary
        });
    }

    [HttpGet("ServiceApplications/{category}")]
    public async Task<ActionResult<List<ServiceApplicationRecord>>> ServiceApplications(string category)
    {
        category = category.Trim().ToLowerInvariant();
        var allowed = new[] { "applications", "new", "inspection", "charging", "payment", "installation", "connect", "tapping", "tickets" };
        if (!allowed.Contains(category)) return BadRequest("Unknown dashboard category.");

        IQueryable<long>? selectedRequestIds = null;
        var stage = category switch
        {
            "new" => "New application",
            "inspection" => "For inspection",
            "charging" => "For charging",
            "payment" => "For payment",
            "installation" => "For installation",
            "connect" => "For connection",
            "tapping" => "For tapping",
            _ => "Service application"
        };

        if (category == "new")
            selectedRequestIds = _context.Requests.Where(x => x.Status == (int)RequestStatus.Draft).Select(x => x.Id);
        else if (category is "inspection" or "charging" or "payment" or "installation")
        {
            var jobStatus = category switch
            {
                "inspection" => JobOrderStatus.Inspection,
                "charging" => JobOrderStatus.Charging,
                "payment" => JobOrderStatus.Payment,
                _ => JobOrderStatus.Installation
            };
            selectedRequestIds = _context.JobOrders.Where(x => x.Status == (int)jobStatus).Select(x => (long)x.RequestId).Distinct();
        }
        else if (category is "connect" or "tapping")
        {
            var pattern = category == "connect" ? "%connect%" : "%tap%";
            selectedRequestIds = from detail in _context.RequestDetails
                                 join type in _context.InspectionTypes on detail.RequestTypeId equals type.Id
                                 where EF.Functions.Like(type.Name, pattern)
                                 select (long)detail.RequestId;
        }

        var baseQuery = from request in _context.Requests.AsNoTracking()
                        join account in _context.Accounts.AsNoTracking() on request.AccountId equals account.Id
                        join customer in _context.Customers.AsNoTracking() on request.CustomerId equals customer.Id
                        select new { request, account, customer };
        if (selectedRequestIds != null)
            baseQuery = baseQuery.Where(x => selectedRequestIds.Contains(x.request.Id));

        var records = await baseQuery
            .OrderByDescending(x => x.request.DateCreated).ThenByDescending(x => x.request.Id)
            .Select(x => new ServiceApplicationRecord
            {
                RequestId = x.request.Id,
                ControlNumber = x.request.ControlNumber,
                AccountNumber = x.account.AccountNumber,
                AccountName = x.customer.Lastname + ", " + x.customer.Firstname + " " + x.customer.Middlename,
                Stage = stage,
                DateSubmitted = x.request.DateCreated
            }).ToListAsync();
        var requestIds = records.Select(x => x.RequestId).ToList();
        var serviceNames = await (from detail in _context.RequestDetails.AsNoTracking()
                                  join type in _context.InspectionTypes.AsNoTracking() on detail.RequestTypeId equals type.Id
                                  where requestIds.Contains(detail.RequestId)
                                  select new { RequestId = (long)detail.RequestId, type.Name })
            .ToListAsync();
        var namesByRequest = serviceNames.GroupBy(x => x.RequestId)
            .ToDictionary(x => x.Key, x => string.Join(", ", x.Select(y => y.Name).Distinct()));
        foreach (var record in records)
            record.ServiceName = namesByRequest.GetValueOrDefault(record.RequestId, "General service");
        return Ok(records);
    }

    [HttpGet("ServiceInspection/{requestId:long}")]
    public async Task<ActionResult<ServiceInspectionRecord>> ServiceInspection(long requestId)
    {
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

    [HttpGet("WorkOrder/{workOrderType}/{requestId:long}")]
    public async Task<ActionResult<ServiceWorkOrderRecord>> WorkOrder(string workOrderType, long requestId)
    {
        workOrderType = workOrderType.Trim().ToLowerInvariant();
        if (workOrderType is not ("installation" or "tapping")) return BadRequest("Unknown work order type.");
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
        record.Materials = await (from material in _context.Materials.AsNoTracking()
                                  join inventory in _context.Inventories.AsNoTracking() on material.InventoryId equals inventory.Id
                                  where material.JobOrderId == selected.job.Id
                                  select new ServiceWorkOrderMaterial { Description = inventory.Name + " (" + inventory.UOM + ")", Quantity = material.RequestedQuantity }).ToListAsync();
        return Ok(record);
    }

    [HttpPost("SaveWorkOrder/{userId:long}")]
    public async Task<ActionResult<ServiceWorkOrderRecord>> SaveWorkOrder(long userId, [FromBody] ServiceWorkOrderRecord record)
    {
        if (record.JobOrderId <= 0 || record.RequestId <= 0) return BadRequest("Job order is required.");
        if (record.WorkOrderType is not ("installation" or "tapping")) return BadRequest("Unknown work order type.");
        if (!record.AssignedToUserId.HasValue) return BadRequest("Assigned personnel is required.");
        if (!record.ScheduledDate.HasValue) return BadRequest("Scheduled date is required.");
        record.AssignedToName = await _context.Users.Where(x => x.Id == record.AssignedToUserId)
            .Select(x => x.Name ?? x.Email ?? string.Empty).FirstOrDefaultAsync() ?? string.Empty;
        record.Personnel = new();
        record.Materials = new();
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
}
