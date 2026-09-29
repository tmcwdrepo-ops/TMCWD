using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{
    [Table("senior_citizen_discounts")]
    public class SeniorCitizenDiscount
    {
        [Key, Column("Id")]
        public long Id { get; set; }

        [Required, MaxLength(200), Column("BillingReferenceId")]
        public string BillingReferenceId { get; set; } = string.Empty;

        [Required, MaxLength(30), Column("SeniorCitizenIdNumber")]
        public string SeniorCitizenIdNumber { get; set; } = string.Empty;

        [Required, MaxLength(160), Column("SeniorCitizenName")]
        public string SeniorCitizenName { get; set; } = string.Empty;

        [Required, MaxLength(160), Column("AccountHolderName")]
        public string AccountHolderName { get; set; } = string.Empty;

        [Column("Rate")]
        public decimal Rate { get; set; }

        [Column("WaterCharge")]
        public decimal WaterCharge { get; set; }

        [Column("DiscountAmount")]
        public decimal DiscountAmount { get; set; }

        [Required, Column("CreatedBy")]
        public long CreatedBy { get; set; }

        [Column("DateCreated")]
        public DateTime DateCreated { get; set; }

        [Column("IsCancelled")]
        public bool IsCancelled { get; set; }

        [Column("CancelledBy")]
        public long CancelledBy { get; set; }

        [Column("DateCancelled")]
        public DateTime? DateCancelled { get; set; }
    }
}
