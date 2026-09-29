using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities;

[Table("penalty_waiver_requests")]
public class PenaltyWaiverRequest
{
    [Key, Column("Id")]
    public long Id { get; set; }

    [Required, MaxLength(200), Column("BillingReferenceId")]
    public string BillingReferenceId { get; set; } = string.Empty;

    [Required, MaxLength(500), Column("Reason")]
    public string Reason { get; set; } = string.Empty;

    [Column("Status")]
    public int Status { get; set; }

    [Column("RequestedBy")]
    public long RequestedBy { get; set; }

    [Column("DateRequested")]
    public DateTime DateRequested { get; set; }

    [Column("ReviewedBy")]
    public long? ReviewedBy { get; set; }

    [Column("DateReviewed")]
    public DateTime? DateReviewed { get; set; }

    [MaxLength(500), Column("ReviewRemarks")]
    public string? ReviewRemarks { get; set; }
}
