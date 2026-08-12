namespace TMCWD.Model.Billing
{
    public class BillingHistory
    {
        public string ReferenceNo { get; set; } = "";

        public DateTime BillingDate { get; set; }

        public decimal Previous { get; set; }

        public decimal Present { get; set; }

        public decimal Usage { get; set; }

        public decimal Amount { get; set; }
    }
}