using Microsoft.AspNetCore.Http.HttpResults;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{
    [Table("advance_payments")]
    public class AdvancePayment
    {
        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Required, Column("AccountId")]
        public System.Int64 AccountId { get; set; }

        [Required, Column("Amount")]
        public decimal Amount { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }

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
