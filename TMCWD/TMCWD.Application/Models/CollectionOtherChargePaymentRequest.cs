using TMCWD.Model.Billing;

namespace TMCWD.Application.Models
{
    /// <summary>
    /// Request body posted by the Other Charges payment modal to
    /// <c>POST /Collections/PayOtherCharges</c>.
    /// </summary>
    [System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
    public class CollectionOtherChargePaymentRequest
    {
        /// <summary>Legacy compatibility only; backend generates the receipt reference.</summary>
        public string InvoiceNumber { get; set; } = string.Empty;
        public string? ExternalReference { get; set; }

        /// <summary>Payment method chosen by the cashier.</summary>
        public PaymentMethod Method { get; set; }

        /// <summary>Amount handed over by the customer.</summary>
        public decimal AmountTendered { get; set; }

        /// <summary>Exact IDs of the collectible installment rows being settled.</summary>
        public List<long> InstallmentIds { get; set; } = new();
    }
}
