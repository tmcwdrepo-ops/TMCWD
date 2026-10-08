using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities;

[Table("payment_receipts")]
public sealed class PaymentReceipt
{
    [Key] public long Id { get; set; }
    [MaxLength(200)] public string? PaymentReference { get; set; }
    public long CreatedBy { get; set; }
    public int Method { get; set; }
    public DateTime DateCreated { get; set; }
    [MaxLength(200)] public string? ExternalReference { get; set; }
}
