using System;
using System.Collections.Generic;
using System.Text;
using TMCWD.Model.Billing.Interfaces;

namespace TMCWD.Model.Billing
{
    public class EWalletTransaction : IeWalletTransaction
    {
        #region constructors

        public EWalletTransaction() { }

        #endregion

        #region properties

        public int Id { get; set; }
        public string PaymentReference { get; set; } = string.Empty;
        public GatewayType Type { get; set; }
        public string Data { get; set; } = string.Empty;
        public int CreatedBy { get; set; }
        public DateTime DateCreated { get; set; }
        public int UpdatedBy { get; set; }
        public DateTime DateUpdated { get; set; }

        #endregion
    }
}
