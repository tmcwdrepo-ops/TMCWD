namespace TMCWD.Model.Dashboard;

public class ServiceDashboard
{
    public int OnlineApplications { get; set; }
    public int NewRequests { get; set; }
    public int Inspection { get; set; }
    public int Charging { get; set; }
    public int Payment { get; set; }
    public int Installation { get; set; }
    public int Connect { get; set; }
    public int Tapping { get; set; }
    public int Tickets { get; set; }
    public List<ServiceRequestSummary> RequestSummary { get; set; } = new();
}

public class ServiceRequestSummary
{
    public string Request { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class ServiceApplicationRecord
{
    public long RequestId { get; set; }
    public string ControlNumber { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public DateTime DateSubmitted { get; set; }
}

public class ServiceInspectionRecord
{
    public long ReportId { get; set; }
    public long RequestId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string ApplicantName { get; set; } = string.Empty;
    public DateTime DateApplied { get; set; }
    public string Address { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string WaterServiceCondition { get; set; } = string.Empty;
    public string PipeSize { get; set; } = string.Empty;
    public string ClassOfPavement { get; set; } = string.Empty;
    public string TypeOfConnection { get; set; } = string.Empty;
    public bool NeedsUndergroundBoring { get; set; }
    public string DistanceFromMainline { get; set; } = string.Empty;
    public int? Zone { get; set; }
    public int? Book { get; set; }
    public string NearestAccountNumber { get; set; } = string.Empty;
    public string NearestAccountName { get; set; } = string.Empty;
    public string InspectionRemarks { get; set; } = string.Empty;
    public long? InspectedByUserId { get; set; }
    public string InspectedByName { get; set; } = string.Empty;
    public long? ApprovedByUserId { get; set; }
    public string ApprovedByName { get; set; } = string.Empty;
    public DateTime? DateInspected { get; set; }
    public string Status { get; set; } = "Pending";
    public bool ChargeNscFee { get; set; } = true;
    public List<ServiceInspectionOption> Inspectors { get; set; } = new();
    public List<ServiceInspectionOption> Approvers { get; set; } = new();
    public List<ServiceInspectionZone> Zones { get; set; } = new();
}

public class ServiceInspectionOption
{
    public long Value { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class ServiceInspectionZone
{
    public int Zone { get; set; }
    public int Book { get; set; }
}

public class ConnectOrderRecord
{
    public long RecommendationId { get; set; }
    public long RequestId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string ApplicantName { get; set; } = string.Empty;
    public DateTime DateApplied { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public string ProcessedByName { get; set; } = string.Empty;
    public string InspectedByName { get; set; } = string.Empty;
    public string InspectionApprovedByName { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime? InvoiceDate { get; set; }
    public long? RecommendedApprovalByUserId { get; set; }
    public string RecommendedApprovalByName { get; set; } = string.Empty;
    public DateTime? DateRecommended { get; set; }
    public long? ApprovedByUserId { get; set; }
    public string ApprovedByName { get; set; } = string.Empty;
    public DateTime? DateApproved { get; set; }
    public List<ServiceInspectionOption> Recommenders { get; set; } = new();
    public List<ServiceInspectionOption> Approvers { get; set; } = new();
}

public class ServiceWorkOrderRecord
{
    public long FindingId { get; set; }
    public long RequestId { get; set; }
    public long JobOrderId { get; set; }
    public string WorkOrderType { get; set; } = string.Empty;
    public string ApplicationNumber { get; set; } = string.Empty;
    public string JobOrderNumber { get; set; } = string.Empty;
    public string ApplicantName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string MeterNumber { get; set; } = string.Empty;
    public string PipeSize { get; set; } = string.Empty;
    public long? AssignedToUserId { get; set; }
    public string AssignedToName { get; set; } = string.Empty;
    public DateTime? ScheduledDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public string WorkLocation { get; set; } = string.Empty;
    public string WorkNotes { get; set; } = string.Empty;
    public string Status { get; set; } = "Scheduled";
    public List<ServiceInspectionOption> Personnel { get; set; } = new();
    public List<ServiceWorkOrderMaterial> Materials { get; set; } = new();
}

public class ServiceWorkOrderMaterial
{
    public string Description { get; set; } = string.Empty;
    public long Quantity { get; set; }
}
