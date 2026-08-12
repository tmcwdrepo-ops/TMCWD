using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace TMCWD.Data.Entities
{
    [Key, Column("Id")]
    public System.Int64 Id { get; set; }

    [Column("DisconnectionDate")]
    public DateTime? DisconnectionDate { get; set; }

    [Column("BillingDate")]
    public DateTime? BillingDate { get; set; }

    [Column("DueDate")]
    public DateTime? DueDate { get; set; }

    [Column("DisconnectionDate")]
    public DateTime? DisconnectionDate { get; set; }

    [Column("BillingPeriodStart")]
    public DateTime? BillingPeriodStart { get; set; }

    [Required, Column("AssignedTo")]
    public System.Int64 AssignedTo { get; set; }

    [Column("SeqFrom")]
    public int? SeqFrom { get; set; }

    [Column("SeqTo")]
    public int? SeqTo { get; set; }

    [Column("Status")]
    public ReadingStatus Status { get; set; } = ReadingStatus.Created;

    [Column("DateCreated")]
    public DateTime? DateCreated { get; set; }

    [Column("DateUpload")]
    public DateTime? DateUpload { get; set; }
}