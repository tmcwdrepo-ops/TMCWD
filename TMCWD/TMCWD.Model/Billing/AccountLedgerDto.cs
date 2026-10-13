namespace TMCWD.Model.Billing;

public sealed class AccountLedgerDto
{
    public sealed class Header
    {
        public string AccountNumber { get; set; } = "";
        public string AccountName { get; set; } = "";
        public string ServiceAddress { get; set; } = "";
        public string? RateCode { get; set; }
        public string MeterNumber { get; set; } = "";
        public string Classification { get; set; } = "";
        public string Status { get; set; } = "";
        public string OperationalAccountStatus { get; set; } = "";
        public string PhysicalServiceStatus { get; set; } = "NO CONFIRMED EVENT";
    }
    public sealed class Bill
    {
        public DateTime BillingPeriod { get; set; }
        public DateTime? ReadingDate { get; set; }
        public DateTime? BillingDate { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public CollectionBill Collection { get; set; } = new();
    }
    public sealed class Charge
    {
        public long Id { get; set; }
        public string Description { get; set; } = "";
        public string Particulars { get; set; } = "";
        public decimal ContractTotal { get; set; }
        public decimal CollectibleNow { get; set; }
        public int? InstallmentCount { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public bool IsActive { get; set; }
        public List<OtherChargeInstallmentRecord> Installments { get; set; } = new();
    }
    public sealed class Event
    {
        public string Key { get; set; } = "";
        public DateTime Date { get; set; }
        public string Type { get; set; } = "";
        public string Reference { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal? Debit { get; set; }
        public decimal? Credit { get; set; }
        public string Status { get; set; } = "";
    }
    public Header Account { get; set; } = new();
    public decimal TotalBalance { get; set; }
    public decimal CollectibleOtherCharges { get; set; }
    public List<Bill> Bills { get; set; } = new();
    public List<Charge> OtherCharges { get; set; } = new();
    public List<Event> History { get; set; } = new();
}
