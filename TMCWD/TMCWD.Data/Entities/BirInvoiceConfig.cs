using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities;

/// <summary>
/// BIR PTU registration configuration used when generating and printing
/// service invoices. Exactly one row should have IsActive = true at any
/// time; enforced at the application layer.
/// </summary>
[Table("bir_invoice_configs")]
public class BirInvoiceConfig
{
    [Key, Column("Id")]
    public long Id { get; set; }

    /// <summary>BIR PTU Number.</summary>
    [Required, MaxLength(100), Column("PTUNumber")]
    public string PTUNumber { get; set; } = string.Empty;

    /// <summary>Date the BIR permit was issued.</summary>
    [Required, Column("PermitDateIssued")]
    public DateOnly PermitDateIssued { get; set; }

    /// <summary>Inclusive start of the authorised invoice series.</summary>
    [Required, Column("SeriesFrom")]
    public long SeriesFrom { get; set; }

    /// <summary>Inclusive end of the authorised invoice series.</summary>
    [Required, Column("SeriesTo")]
    public long SeriesTo { get; set; }

    [MaxLength(10), Column("InvoicePrefix")]
    public string? InvoicePrefix { get; set; }

    [Column("SeriesWidth")]
    public int? SeriesWidth { get; set; }

    /// <summary>Whether this is the currently active configuration.</summary>
    [Required, Column("IsActive")]
    public bool IsActive { get; set; }

    /// <summary>Earliest billing date this config applies to (optional).</summary>
    [Column("EffectiveFrom")]
    public DateOnly? EffectiveFrom { get; set; }

    /// <summary>Last billing date this config applies to (optional).</summary>
    [Column("EffectiveTo")]
    public DateOnly? EffectiveTo { get; set; }

    [Column("CreatedBy")]
    public long CreatedBy { get; set; }

    [Column("DateCreated")]
    public DateTime DateCreated { get; set; }

    [Column("UpdatedBy")]
    public long UpdatedBy { get; set; }

    [Column("DateUpdated")]
    public DateTime DateUpdated { get; set; }
}
