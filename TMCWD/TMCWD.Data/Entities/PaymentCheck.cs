using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{
    [Table("payment_checks")]
    public class PaymentCheck
    {

        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Required, MaxLength(200), Column("PaymentReference")]
        public string PaymentReference { get; set; } = string.Empty;

        [Required, MaxLength(200), Column("Branch")]
        public string Branch { get; set; } = string.Empty;

        [Required, MaxLength(200), Column("CheckNumber")]
        public string CheckNumber { get; set; } = string.Empty;

        [Required, Column("Amount")]
        public decimal Amount { get; set; }

        [Required, Column("CheckDate")]
        public DateTime CheckDate { get; set; }

        [Required, Column("CreatedBy")]
        public System.Int64 CreatedBy { get; set; }

        [Column("DateCreated")]
        public DateTime DateCreated { get; set; }

        [Column("UpdatedBy")]
        public System.Int64 UpdatedBy { get; set; }

        [Column("DateUpdated")]
        public DateTime DateUpdated { get; set; }

    }
}
