using TMCWD.Model.Billing;

namespace TMCWD.Application.Models
{
    /// <summary>
    /// Request body posted by the Other Charges payment modal to
    /// <c>POST /Collections/PayOtherCharges</c>.
    /// </summary>
    public class CollectionOtherChargePaymentRequest
    {
        /// <summary>Unique OR / invoice number generated on the client.</summary>
        public string InvoiceNumber { get; set; } = string.Empty;

        /// <summary>Payment method chosen by the cashier.</summary>
        public PaymentMethod Method { get; set; }

        /// <summary>Amount handed over by the customer.</summary>
        public decimal AmountTendered { get; set; }

        /// <summary>IDs of the <c>other_charges</c> rows being settled.</summary>
        public List<int> OtherChargeIds { get; set; } = new();
    }
}
