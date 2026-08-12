using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{
    public class Billing
    {

        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Required, Column("AccountId")]
        public System.Int64 AccountId { get; set; }

        [Column("JobOrderId")]
        public System.Int64 JobOrderId { get; set; }

        [Required, Column("BillingReferenceId")]
        public string BillingReferenceId { get; set; } = string.Empty;

        [Column("PaymentTransactionId")]
        public string PaymentTransactionId { get; set; } = string.Empty;

        [Column("ReadingId")]
        public System.Int64 ReadingId { get; set; }

        [Column("BillingPeriodf")]
        public DateTime BillingPeriod { get; set; }

        [Column("MaterialsAmount")]
        public decimal MaterialsAmount { get; set; }

        [Column("Penalties")]
        public decimal Penalties { get; set; }

        [Column("OtherCharges")]
        public decimal OtherCharges { get; set; }

        [Column("BillingAdjustment")]
        public decimal BillingAdjustment { get; set; }

        [Required, Column("TotalBillAmount")]
        public decimal TotalBillAmount { get; set; }

        [Required, Column("Status")]
        public int PaymentStatus { get; set; }

        [Column("CreatedBy")]
        public System.Int64 CreatedBy { get; set; }

        [Column("DateCreated")]
        public DateTime DateCreated { get; set; }

        [Column("UpdatedBy")]
        public System.Int64 UpdatedBy { get; set; }

        [Column("DateUpdated")]
        public DateTime DateUpdated { get; set; }

        [Column("RemainingAmount")]
        public decimal RemainingAmount { get; set; }

    }
}
