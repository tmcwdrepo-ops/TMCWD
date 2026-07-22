using System;
using System.Collections.Generic;
using System.Text;
using TMCWD.Model.Administrator;
using TMCWD.Model.Billing.Interfaces;
using TMCWD.Services;

namespace TMCWD.Billing
{
    public class CashPayment : PaymentBase
    {

        #region fields

        private WebService service = new();

        #endregion

        #region constructors

        public CashPayment(TMCWD.Model.Billing.Billing bill) : base(bill, PaymentMethod.Cash) { }

        #endregion

        #region methods

        public override async Task<bool> ProcessPayment(User user)
        {
            bool isSuccess = false;
            try
            {
                if (!this.CheckData()) return false;

                decimal newUnpaid = this._billing.UnpaidAmount - (this.PaidAmount + this._billing.AdvancePayment.Amount);
                if(newUnpaid <= 0) this._billing.PaymentStatus = PaymentStatus.Paid;

                if (!this.IsKeepChangeToAdvancePayment) this._billing.AdvancePayment.Amount = 0;

                this.Success = true;

                PaymentTransaction payTransaction = new(service);
                AdvancePaymentTransaction advancePaymentTransaction = new(service);
                BillingTransaction billingTransaction = new(service);

                Task<PaymentBase> savePayment = payTransaction.SaveUpdate(user.Id, this);
                Task<TMCWD.Model.Billing.AdvancePayment> updateAdvancePayment = advancePaymentTransaction.SaveUpdate(user.Id, this._billing.AdvancePayment);
                Task<TMCWD.Model.Billing.Interfaces.BillingBase> updateBilling = billingTransaction.SaveUpdate(user.Id, this._billing);

                await Task.WhenAll(savePayment, updateAdvancePayment, updateBilling);
                if(savePayment.Result.Id > 0 && updateAdvancePayment.Id > 0 && updateAdvancePayment.Id > 0) isSuccess = true;

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
