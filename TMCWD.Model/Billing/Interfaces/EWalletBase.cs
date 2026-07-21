using System;
using System.Collections.Generic;
using System.Text;
using TMCWD.Model.Administrator;

namespace TMCWD.Model.Billing.Interfaces
{
    public abstract class EWalletBase
    {

        #region constructors

        public EWalletBase() { }

        #endregion

        #region properties

        public int Id { get; set; }

        public string PaymentReference { get; set; } = string.Empty;

        public string Data { get; set; } = string.Empty;

        public int CreatedBy { get; set; }

        public DateTime DateCreated { get; set; }

        public int UpdatedBy { get; set; }

        public DateTime DateUpdated { get; set; }

        #endregion

        #region methods

        public abstract Task<bool> SendPayment(User user, PaymentBase payment);

        #endregion

    }
}
