namespace TMCWD.Model.Billing;

public sealed class CollectionBill
{
    public string BillingReferenceId { get; init; } = string.Empty;
    public string? ServiceInvoiceNumber { get; init; }
    public string? PaymentReference { get; init; }
    public decimal? BillAmount { get; init; }
    public decimal? MeterMaintenanceFee { get; init; }
    public decimal OtherCharges { get; init; }
    public decimal BillingAdjustment { get; init; }
    public decimal RemainingAmount { get; init; }
    public decimal Penalty { get; init; }
    public decimal AmountDue { get; init; }
    public decimal? TotalDue { get; init; }
    public decimal? PenaltyAfterDue { get; init; }
    public decimal? TotalAfterDue { get; init; }
    public DateTime BillingDate { get; init; }
    public DateTime? DueDate { get; init; }
    public DateTime? DisconnectionDate { get; init; }
    public DateTime? PeriodFrom { get; init; }
    public DateTime? PeriodTo { get; init; }
}
