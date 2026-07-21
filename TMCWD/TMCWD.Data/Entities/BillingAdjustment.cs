namespace TMCWD.Data.Entities
{
    public class BillingAdjustment
    {

        public System.Int64 Id { get; set; }

        public System.Int64 Type { get; set; }

        public string BillingReferenceId { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public System.Int64 CreatedBy { get; set; }

        public DateTime DateCreated { get; set; }

        public System.Int64 UpdatedBy { get; set; }

        public DateTime DateUpdated { get; set; }

    }
}
