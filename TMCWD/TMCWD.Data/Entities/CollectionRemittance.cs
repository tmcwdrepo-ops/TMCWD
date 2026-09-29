using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities;

[Table("collection_remittances")]
public sealed class CollectionRemittance
{
    [Key]
    public long Id { get; set; }
    public DateTime Date { get; set; }
    public long CollectorId { get; set; }
    public string OrStart { get; set; } = string.Empty;
    public string OrEnd { get; set; } = string.Empty;
    public bool IsDeposit { get; set; }
    public string CountsJson { get; set; } = "{}";
    public decimal OtherCharges { get; set; }
    public decimal BillsTotal { get; set; }
    public decimal TotalCollection { get; set; }
    public decimal CheckAmount { get; set; }
    public decimal OnlineAmount { get; set; }
    public decimal CashOnHand { get; set; }
    public decimal CountedCash { get; set; }
    public decimal ShortOver { get; set; }
    public DateTime DateCreated { get; set; }
}
