using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{
    [Table("payments")]
    public class Payment
    {

        public Payment() { }

        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Required, MaxLength(200), Column("BillingReference")]
        public string BillingReference { get; set; } = string.Empty;

        [Required, MaxLength(200), Column("PaymentReferencef")]
        public string PaymentReference { get; set; } = string.Empty;

        [Required, MaxLength(100), Column("MacAddress")]
        public string MacAddress { get; set; } = string.Empty;

        [Column("PaidAmount")]
        public decimal PaidAmount { get; set; }

        [Required, Column("Method")]
        public int Method { get; set; }

        [Column("Success")]
        public bool Success { get; set; }

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
