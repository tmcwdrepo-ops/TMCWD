using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TMCWD.Model.Administrator;
using TMCWD.Model.Billing.Interfaces;
using TMCWD.Services;

namespace TMCWD.Billing
{
    public class EWalletPayment : PaymentBase
    {

        #region fields

        private WebService service = new();

        #endregion

        #region constructors

        public EWalletPayment(TMCWD.Model.Billing.Billing bill) : base(bill, PaymentMethod.EWallet) { }

        #endregion

        #region properties

        public GatewayType PaymentType { get; set; }

        #endregion

        #region method

        public override async Task<bool> ProcessPayment(User user)
        {
            bool isSuccess = false;
            try
            {
                if (!this.CheckData()) return false;

                decimal newUnpaid = this._billing.UnpaidAmount - (this.PaidAmount + this._billing.AdvancePayment.Amount);
                if (newUnpaid <= 0) this._billing.PaymentStatus = PaymentStatus.Paid;

                if (!this.IsKeepChangeToAdvancePayment) this._billing.AdvancePayment.Amount = 0;

                this.Success = false;

                PaymentTransaction payTransaction = new(service);
                AdvancePaymentTransaction advancePaymentTransaction = new(service);
                BillingTransaction billingTransaction = new(service);

                Task<PaymentBase> savePayment = payTransaction.SaveUpdate(user.Id, this);
                Task<TMCWD.Model.Billing.AdvancePayment> updateAdvancePayment = advancePaymentTransaction.SaveUpdate(user.Id, this._billing.AdvancePayment);
                Task<TMCWD.Model.Billing.Interfaces.BillingBase> updateBilling = billingTransaction.SaveUpdate(user.Id, this._billing);

                await Task.WhenAll(savePayment, updateAdvancePayment, updateBilling);
                if (savePayment.Result.Id > 0 && updateAdvancePayment.Id > 0 && updateAdvancePayment.Id > 0) isSuccess = true;

            }
            catch
            {
                throw;
            }

            return isSuccess;
        }

        #endregion

    }
}
