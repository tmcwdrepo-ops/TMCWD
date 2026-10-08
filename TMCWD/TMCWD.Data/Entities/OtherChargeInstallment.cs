using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities;

[Table("other_charge_installments")]
public class OtherChargeInstallment
{
    public long Id { get; set; }
    public long OtherChargeId { get; set; }
    public int InstallmentNumber { get; set; }
    public DateTime DueBillingPeriod { get; set; }
    [Column(TypeName = "decimal(6,2)")]
    public decimal Amount { get; set; }
    public int PaymentStatus { get; set; }
    public long? SettledPaymentId { get; set; }
    public string? ActivatedBillingReferenceId { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public long CreatedBy { get; set; }
    public DateTime DateCreated { get; set; }
    public long UpdatedBy { get; set; }
    public DateTime DateUpdated { get; set; }
}
