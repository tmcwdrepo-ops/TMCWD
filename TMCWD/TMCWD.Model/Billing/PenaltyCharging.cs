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
}

public class PostPenaltyChargesRequest
{
    public DateTime? BillingPeriod { get; set; }
    public List<string> BillingReferenceIds { get; set; } = new();
}

public class PostPenaltyChargesResult
{
    public int PostedCount { get; set; }
    public decimal TotalPenalty { get; set; }
    public DateTime PostingDate { get; set; }
}
