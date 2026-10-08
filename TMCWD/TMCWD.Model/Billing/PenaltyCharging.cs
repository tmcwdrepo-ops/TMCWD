namespace TMCWD.Model.Billing;

public static class LatePaymentPenalty
{
    public static DateTime Today => DateTime.UtcNow.AddHours(8).Date;

    public static decimal Calculate(decimal waterBill) =>
        Math.Round(Math.Max(0, waterBill) * 0.10m, 2, MidpointRounding.AwayFromZero);

}

public class PenaltyChargeEntry
{
    public string BillingReferenceId { get; set; } = string.Empty;
    public string AccountNo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime BillingDate { get; set; }
    public DateTime DueDate { get; set; }
    public decimal Usage { get; set; }
    public decimal BillAmount { get; set; }
    public decimal Discount { get; set; }
    public decimal Penalty { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string? InvoiceSnapshotJson { get; set; }
}

public class ActivePenaltyEntry
{
    public string BillingReferenceId { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public decimal Usage { get; set; }
    public decimal BillAmount { get; set; }
    public decimal Discount { get; set; }
    public decimal Penalty { get; set; }
}

public class PostPenaltyChargesRequest
{
    public DateTime? BillingPeriod { get; set; }
    /// <summary>
    /// Include all bills with DueDate on or before this date.
    /// Defaults to yesterday (today − 1) when not provided.
    /// </summary>
    public DateTime? DueDate { get; set; }
    public DateTime? PostingDate { get; set; }
    public List<string> BillingReferenceIds { get; set; } = new();
}

public class PostPenaltyChargesResult
{
    public int PostedCount { get; set; }
    public decimal TotalPenalty { get; set; }
    public DateTime PostingDate { get; set; }
}
