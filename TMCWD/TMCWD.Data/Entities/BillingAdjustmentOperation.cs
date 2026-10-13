using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities;

[Table("billing_adjustment_operations")]
public sealed class BillingAdjustmentOperation
{
    [Key, MaxLength(36)] public string OperationId { get; set; } = "";
    [MaxLength(50)] public string BillingReferenceId { get; set; } = "";
    public long ActorId { get; set; }
    [Column(TypeName = "char(64)")] public string PayloadHash { get; set; } = "";
    [Column(TypeName = "longtext")] public string ResultJson { get; set; } = "";
    public DateTime DateCreated { get; set; }
}
