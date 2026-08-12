using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{
    [Table("billing_adjustments")]
    public class BillingAdjustment
    {
        public long Id { get; set; }

        public long Type { get; set; }

        public string BillingReferenceId { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public long CreatedBy { get; set; }

        public DateTime DateCreated { get; set; }

        public long UpdatedBy { get; set; }

        public DateTime DateUpdated { get; set; }
    }
}