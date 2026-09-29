using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities;

[Table("invoice_cancellations")]
public sealed class InvoiceCancellation
{
    [Key, Column("Id")]
    public long Id { get; set; }

    [Required, MaxLength(200), Column("PaymentReference")]
    public string PaymentReference { get; set; } = string.Empty;

    [Required, MaxLength(200), Column("Reason")]
    public string Reason { get; set; } = string.Empty;

    [Required, Column("CancelledBy")]
    public long CancelledBy { get; set; }

    [Column("DateCancelled")]
    public DateTime DateCancelled { get; set; }
}
