namespace TMCWD.Model.Billing;

public sealed class PaymentHistoryRecord
{
    public string BillingReference { get; set; } = string.Empty;
    public string PaymentReference { get; set; } = string.Empty;
    public decimal PaidAmount { get; set; }
    public int Method { get; set; }
    public bool Success { get; set; }
    public long CreatedBy { get; set; }
    public DateTime DateCreated { get; set; }
}
