namespace TMCWD.Model.Billing.Requests
{
    /// <summary>
    /// Payload sent by the Other Charges modal when the cashier clicks Pay.
    /// </summary>
    [System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
    public class OtherChargePaymentRequest
    {
        /// <summary>Legacy input retained for compatibility; the server allocates the receipt reference.</summary>
        public string InvoiceNumber { get; set; } = string.Empty;
        public string? ExternalReference { get; set; }

        /// <summary>Payment method: Cash, Check, or EWallet.</summary>
        public PaymentMethod Method { get; set; }

        /// <summary>Cash / check amount handed over by the customer.</summary>
        public decimal AmountTendered { get; set; }

        /// <summary>
        /// Exact IDs of the collectible installment rows being settled.
        /// </summary>
        public List<long> InstallmentIds { get; set; } = new();
    }
}
