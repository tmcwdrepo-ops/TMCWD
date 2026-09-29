namespace TMCWD.Model.Billing;

public class SubmitPenaltyWaiverRequest
{
    public List<string> BillingReferenceIds { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
}

public class ReviewPenaltyWaiverRequest
{
    public long RequestId { get; set; }
    public bool Approve { get; set; }
    public string Remarks { get; set; } = string.Empty;
}

public class PenaltyWaiverRequestRecord
{
    public long Id { get; set; }
    public string BillingReferenceId { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public decimal PenaltyAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int Status { get; set; }
    public long RequestedBy { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public DateTime DateRequested { get; set; }
    public long? ReviewedBy { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? DateReviewed { get; set; }
    public string? ReviewRemarks { get; set; }
}
