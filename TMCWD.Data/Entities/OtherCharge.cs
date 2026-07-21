using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography.X509Certificates;

namespace TMCWD.Data.Entities
{

    [Table("other_charges")]
    public class OtherCharge
    {

        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Column("BillingReferenceId")]
        public string BillingReferenceId { get; set; } = string.Empty;

        [Column("Type")]
        public int Type { get; set; }

        [Column("Amount")]
        public decimal Amount { get; set; }

        [Column("PaymentStatus")]
        public int PaymentStatus { get; set; }

        [Column("CreatedBy")]
        public System.Int64 CreatedBy { get; set; }

        [Column("DateCreated")]
        public DateTime DateCreated { get; set; }

        [Column("UpdatedBy")]
        public System.Int64 UpdatedBy { get; set; }

        [Column("UpdatedBy")]
        public DateTime DateUpdated { get; set; }

    }
}
