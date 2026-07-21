using System;
using System.Collections.Generic;
using System.Text;
using TMCWD.Model.Billing.Interfaces;

namespace TMCWD.Model.Billing
{
    public class PaymentCheck : IPaymentCheck
    {

        #region constructors
        public PaymentCheck() { }
        #endregion

        #region properties

        public int Id { get; set; }
        public string BillingReference { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string CheckNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime CheckDate { get; set; }
        public long CreatedBy { get; set; }
        public DateTime DateCreated { get; set; }
        public long UpdatedBy { get; set; }
        public DateTime DateUpdated { get; set; }

        #endregion

    }
}
