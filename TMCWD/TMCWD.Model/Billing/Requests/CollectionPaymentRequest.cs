namespace TMCWD.Model.Billing.Requests
{
    public class CollectionPaymentRequest
    {
        // Legacy compatibility only; never used to allocate or select a payment receipt.
        public string InvoiceNumber { get; set; } = string.Empty;
        public string? ExternalReference { get; set; }
        public PaymentMethod Method { get; set; }
        public decimal AmountTendered { get; set; }
        public List<CollectionPaymentBillRequest> Bills { get; set; } = new();
        public SeniorCitizenDiscountRequest? SeniorCitizenDiscount { get; set; }
    }

    public class CollectionPaymentBillRequest
    {
        public string BillingReferenceId { get; set; } = string.Empty;
        public decimal PaidAmount { get; set; }
        public List<long> InstallmentIds { get; set; } = new();
    }

    public class SeniorCitizenDiscountRequest
    {
        public string IdNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
