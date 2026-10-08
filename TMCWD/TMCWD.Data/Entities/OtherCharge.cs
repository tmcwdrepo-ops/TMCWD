using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography.X509Certificates;

namespace TMCWD.Data.Entities
{

    [Table("other_charges")]
    public class OtherCharge
    {
        public long? SettledPaymentId { get; set; }
        // Posted plans require a positive count and exactly this many children.
        public int? InstallmentCount { get; set; }
        [NotMapped]
        public List<TMCWD.Model.Billing.OtherChargeInstallmentRecord> Installments { get; set; } = new();

        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Column("BillingReferenceId")]
        public string BillingReferenceId { get; set; } = string.Empty;

        [Column("Type")]
        public int Type { get; set; }

        [Column("PayableIn")]
        public string? PayableIn { get; set; } = string.Empty;

        [Column("Particulars")]
        public string Particulars { get; set; } = string.Empty;

        [Column("Amount")]
        public decimal Amount { get; set; }

        [Column("PaymentStatus")]
        public int PaymentStatus { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; } = true;

        [Column("CreatedBy")]
        public System.Int64 CreatedBy { get; set; }

        [Column("DateCreated")]
        public DateTime DateCreated { get; set; }

        [Column("UpdatedBy")]
        public System.Int64 UpdatedBy { get; set; }

        [Column("DateUpdated")]
        public DateTime DateUpdated { get; set; }

    }
}
