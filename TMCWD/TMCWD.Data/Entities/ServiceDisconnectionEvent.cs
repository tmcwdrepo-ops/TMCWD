using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities;

[Table("service_disconnection_events")]
public sealed class ServiceDisconnectionEvent
{
    [Key, MaxLength(36)] public string Id { get; set; } = string.Empty;
    public long AccountId { get; set; }
    public long FirstBillingId { get; set; }
    public long SecondBillingId { get; set; }
    [MaxLength(50)] public string FirstBillingReference { get; set; } = string.Empty;
    [MaxLength(50)] public string SecondBillingReference { get; set; } = string.Empty;
    public DateTime SubjectDate { get; set; }
    public DateTime ScheduledDisconnectionDate { get; set; }
    public DateTime ActualDisconnectionDateUtc { get; set; }
    public long DisconnectedBy { get; set; }
    public long ReconnectionFeeOtherChargeId { get; set; }
    public DateTime? ReconnectionEligibleDateUtc { get; set; }
    public DateTime? ActualReconnectionDateUtc { get; set; }
    public long? ReconnectedBy { get; set; }
    [MaxLength(36)] public string? ReconnectionOperationId { get; set; }
    [Column(TypeName = "longtext")] public string? SettlementPaymentIdsJson { get; set; }
    public long? ActiveAccountId { get; private set; }
}
