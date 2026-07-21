using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{

    [Table("penalties")]
    public class Penalty
    {

        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Required, Column("BillingReferenceId")]
        public string BillingReferenceId { get; set; } = string.Empty;

        [Column("Amount")]
        public decimal Amount { get; set; }

        [Column("PaymentStatus")]
        public int PaymentStatus { get; set; }

        [Column("PenaltyTypeId")]
        public System.Int64 PenaltyTypeId { get; set; }

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
