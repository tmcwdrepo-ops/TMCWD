namespace TMCWD.Model.Billing.Requests;

public sealed class CollectionRemittanceRequest
{
    public DateTime Date { get; set; }
    public int CollectorId { get; set; }
    public string OrStart { get; set; } = string.Empty;
    public string OrEnd { get; set; } = string.Empty;
    public bool IsDeposit { get; set; }
    public Dictionary<string, decimal> Counts { get; set; } = new();
    public decimal OtherCharges { get; set; }
    public decimal BillsTotal { get; set; }
    public decimal TotalCollection { get; set; }
    public decimal CheckAmount { get; set; }
    public decimal OnlineAmount { get; set; }
    public decimal CashOnHand { get; set; }
    public decimal CountedCash { get; set; }
    public decimal ShortOver { get; set; }
}
