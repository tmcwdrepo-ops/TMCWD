namespace TMCWD.Model.Billing.Requests
{
    public class CollectionPaymentRequest
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public PaymentMethod Method { get; set; }
        public decimal AmountTendered { get; set; }
        public List<CollectionPaymentBillRequest> Bills { get; set; } = new();
    }

    public class CollectionPaymentBillRequest
    {
        public string BillingReferenceId { get; set; } = string.Empty;
        public decimal PaidAmount { get; set; }
    }
}
