using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{
    [Table("billing_adjustments")]
    public class BillingAdjustment
    {
        public long Id { get; set; }

        public long Type { get; set; }

        [MaxLength(50)]
        public string BillingReferenceId { get; set; } = string.Empty;

        [Column(TypeName = "decimal(6,2)")]
        public decimal Amount { get; set; }

        /// <summary>
        /// Optional free-text reason captured at submission time.
        /// NULL for historical rows that pre-date Phase 6I-C1 audit history.
        /// </summary>
        [MaxLength(255)]
        public string? Reason { get; set; }

        public long CreatedBy { get; set; }

        public DateTime DateCreated { get; set; }

        public long UpdatedBy { get; set; }

        public DateTime DateUpdated { get; set; }
    }
}
