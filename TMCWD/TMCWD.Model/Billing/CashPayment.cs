using System;
using System.Collections.Generic;
using System.Text;
using TMCWD.Model.Billing.Interfaces;

namespace TMCWD.Model.Billing
{
    public class CashPayment : PaymentBase
    {

        #region constructors

        public CashPayment() : base(PaymentMethod.Cash) { }

        #endregion

        #region methods

        public override bool ProcessPayment()
        {

            return true;
        }

        #endregion

    }
}
