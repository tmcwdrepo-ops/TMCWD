namespace TMCWD.Model.Billing;

public sealed class InvoiceRecord
{
    public string InvoiceNo { get; set; } = string.Empty;
    public string Account { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public DateTime DateIssued { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CancellationReason { get; set; } = string.Empty;
    public List<InvoiceLineRecord> Lines { get; set; } = new();
}

public sealed class InvoiceLineRecord
{
    public string BillingReference { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public sealed class CancelInvoiceRequest
{
    public string InvoiceNo { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
