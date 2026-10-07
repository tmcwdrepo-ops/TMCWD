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
    public long? AccountId { get; set; }
    public string ControlNumber { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
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

    /// <summary>Meter serial number — captured at inspection time on the physical form.</summary>
    public string MeterNumber { get; set; } = string.Empty;

    /// <summary>Meter size in inches (½, ¾, 1, 1½, 2). Default 0.5.</summary>
    public decimal MeterSize { get; set; } = 0.5m;

    /// <summary>
    /// Populated by the server when the inspection is Approved and the account
    /// is created. Sent back to the client so it can be printed on the PDF.
    /// </summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>Zone from the original new connection application — read-only in the inspection form.</summary>
    public int? ApplicationZone { get; set; }

    /// <summary>Book from the original new connection application — read-only in the inspection form.</summary>
    public int? ApplicationBook { get; set; }
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

    /// <summary>Populated after account activation — shown in the connect modal.</summary>
    public string AccountNumber { get; set; } = string.Empty;
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

public class HouseTappingRecord
{
    public long RequestId { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string ApplicantName { get; set; } = string.Empty;
    public DateTime? DateApplied { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public string InspectedByName { get; set; } = string.Empty;
    public DateTime? DateInspected { get; set; }
    public int? Zone { get; set; }
    public int? Book { get; set; }
    public string MeterBrand { get; set; } = string.Empty;
    public decimal MeterSize { get; set; } = 0.5m;
    public string SerialNumber { get; set; } = string.Empty;
    public decimal InitialReading { get; set; }
    public long? AccomplishedByUserId { get; set; }
    public string AccomplishedByName { get; set; } = string.Empty;
    public DateTime? DateAccomplished { get; set; }
    public string Remarks { get; set; } = string.Empty;
    public List<ServiceInspectionZone> ZoneBooks { get; set; } = new();
    public List<string> MeterBrands { get; set; } = new();
    public List<ServiceInspectionOption> Personnel { get; set; } = new();
}

/// <summary>
/// Read-only payment status view for an application at the payment stage.
/// Pulled directly from water_connection_applications columns.
/// </summary>
public class ApplicationPaymentRecord
{
    public long ApplicationId { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Barangay { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string ConnectionType { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }

    // Parsed from charging_data / payment_data JSON blobs (nullable — may not exist yet)
    public string? ChargeReference { get; set; }
    public decimal? ChargeAmount { get; set; }
    public string? PaymentReference { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? PaymentStatus { get; set; }
    public string? Remarks { get; set; }
}

/// <summary>
/// Full read-only detail view for an application — used by the Tickets card.
/// </summary>
public class ApplicationTicketRecord
{
    public long ApplicationId { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Barangay { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ConnectionType { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string? Remarks { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Stage data blobs — raw JSON strings; front-end parses what it needs
    public string? InspectionData { get; set; }
    public string? ChargingData { get; set; }
    public string? PaymentData { get; set; }
    public string? InstallationData { get; set; }
    public string? ConnectData { get; set; }
    public string? TappingData { get; set; }
    public string? DoneData { get; set; }
}

public class NewConnectionApplicationRecord
{
    public long ApplicationId { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public DateTime? DateApplied { get; set; }
    public string ApplicantName { get; set; } = string.Empty;
    public string Suffix { get; set; } = string.Empty;
    public string Barangay { get; set; } = string.Empty;
    /// <summary>Combined address — kept for backward compatibility. Use the split fields for new accounts.</summary>
    public string Address { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
    public string Building { get; set; } = string.Empty;
    public string HouseNumber { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string ConsumerType { get; set; } = "residential";
    public string ServiceSize { get; set; } = string.Empty;
    public int? Zone { get; set; }
    public int? Book { get; set; }
    public string NearestAccountNumber { get; set; } = string.Empty;
    public string NearestAccountName { get; set; } = string.Empty;
    public string SpouseName { get; set; } = string.Empty;
    public int? PeopleInHousehold { get; set; }
    public string Establishment { get; set; } = string.Empty;
    public string PlaceOfBirth { get; set; } = string.Empty;
    public DateTime? Birthdate { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string CivilStatus { get; set; } = string.Empty;
    public string NatureOfBusiness { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public string OwnershipStatus { get; set; } = string.Empty;
    public bool SameAsApplicant { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerAddress { get; set; } = string.Empty;
    public string MeterLocation { get; set; } = string.Empty;
    public string ConnectionNotes { get; set; } = string.Empty;
    public bool HasValidId { get; set; }
    public bool HasProofOfOwnership { get; set; }
    public bool HasBarangayClearance { get; set; }
    public bool HasLocationSketch { get; set; }
    public string PhotoFileName { get; set; } = string.Empty;
}

public class DisconnectionAccountRecord
{
    public long AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string MeterNumber { get; set; } = string.Empty;
    public string AccountStatus { get; set; } = string.Empty;
    public decimal? LastReading { get; set; }
}

public class DisconnectionOrderRecord
{
    public long OrderId { get; set; }
    public long AccountId { get; set; }
    public string JobOrderNumber { get; set; } = string.Empty;
    public DateTime? JobOrderDate { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string MeterNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? DisconnectionDate { get; set; }
    public string NatureOfDisconnection { get; set; } = string.Empty;
    public string Possession { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal? LastReading { get; set; }
    public string Plumber { get; set; } = string.Empty;
    public decimal ReconnectionFee { get; set; } = 350m;
    public string Remarks { get; set; } = string.Empty;
    public bool ChargeFee { get; set; } = true;
    public bool ReadingOverride { get; set; }
    public string Action { get; set; } = "disconnect";
}

public class AccountTransferLookupRecord
{
    public long AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int Zone { get; set; }
    public int Book { get; set; }
}

public class AccountTransferRecord
{
    public long TransferId { get; set; }
    public long AccountId { get; set; }
    public string JobOrderNumber { get; set; } = string.Empty;
    public DateTime? JobOrderDate { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int Zone { get; set; }
    public int Book { get; set; }
    public string NewAccountNumber { get; set; } = string.Empty;
    public DateTime? TransferDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class ServiceRequestAssigneeRecord
{
    public long UserId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ServiceRequestOrderRecord
{
    public bool IsReadOnly { get; set; }
    public long RequestId { get; set; }
    public long ApplicationId { get; set; }
    public bool IsNonCustomer { get; set; }
    public long? AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ReportedBy { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string JobOrderNumber { get; set; } = string.Empty;
    public DateTime? JobOrderDate { get; set; }
    public string Source { get; set; } = string.Empty;
    public string JobOrderType { get; set; } = string.Empty;
    public string JobDescription { get; set; } = string.Empty;
    public long? ReferredToUserId { get; set; }
    public string ReferredToDepartment { get; set; } = string.Empty;
    public string ReferredToName { get; set; } = string.Empty;
    public DateTime? DateReferred { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Particulars { get; set; } = string.Empty;
    public string ReceivedByName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

/// <summary>Request body for the ActivateAccount endpoint.</summary>
public class ActivateAccountRequest
{
    public long ApplicationId { get; set; }

    /// <summary>Override zone if not stored in inspection_data.</summary>
    public int Zone { get; set; }

    /// <summary>Override book if not stored in inspection_data.</summary>
    public int Book { get; set; }

    /// <summary>Meter number assigned during installation.</summary>
    public string? MeterNumber { get; set; }

    /// <summary>Meter size in inches (default 0.5).</summary>
    public decimal MeterSize { get; set; } = 0.5m;
}

/// <summary>Response from ActivateAccount.</summary>
public class ActivateAccountResult
{
    public long ApplicationId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public long AccountId { get; set; }

    /// <summary>True if the account was already created in a prior call.</summary>
    public bool AlreadyActivated { get; set; }
}

public class AdvanceStageRequest
{
    public long ApplicationId { get; set; }
    /// <summary>Optional — the caller's expected current stage (for optimistic concurrency check).</summary>
    public string? ExpectedCurrentStage { get; set; }
}

public class AdvanceStageResult
{
    public long ApplicationId { get; set; }
    public string PreviousStage { get; set; } = string.Empty;
    public string NewStage { get; set; } = string.Empty;
}

public class ServiceRequestSummaryRow
{
    public bool IsReadOnly { get; set; }
    public string MeterNumber { get; set; } = string.Empty;
    public decimal? LastReading { get; set; }
    public string Source { get; set; } = string.Empty;
    public string ReceivedByName { get; set; } = string.Empty;
    public string ReferredToName { get; set; } = string.Empty;
    public DateTime? DateReferred { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Particulars { get; set; } = string.Empty;
    public long RequestId { get; set; }
    public string JobOrderNumber { get; set; } = string.Empty;
    public DateTime JobOrderDate { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ReportedBy { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string JobOrderType { get; set; } = string.Empty;
    public string JobDescription { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
