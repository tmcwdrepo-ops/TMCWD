using System;
using System.Collections.Generic;
using System.Text;
using TMCWD.Model.Administrator;
using TMCWD.Model.Billing;
using TMCWD.Model.Billing.Interfaces;
using TMCWD.Services;

namespace TMCWD.Billing
{
    public class CheckPayment : PaymentBase
    {


        #region fields

        private WebService service = new();

        #endregion

        #region constructors

        public CheckPayment(TMCWD.Model.Billing.Billing bill) : base(bill, PaymentMethod.Check) { }

        #endregion

        #region properties

        public int BankId { get; set; }

        public string Branch { get; set; } = string.Empty;

        public string CheckNumber { get; set; } = string.Empty;

        public decimal CheckAmount { get; set; }

        public DateTime CheckDate { get; set; }

        #endregion

        #region methods

        public override async Task<bool> ProcessPayment(User user)
        {
            bool isSuccess = false;

            try
            {

                if (this.CheckData())
                {

                    if (this.PaidAmount > this.CheckAmount) throw new Exception("Check amount is not sufficient to the desired amount to pay");

                    decimal newUnpaid = this._billing.UnpaidAmount - (this.PaidAmount + this._billing.AdvancePayment.Amount);
                    if (newUnpaid <= 0) this._billing.PaymentStatus = PaymentStatus.Paid;

                    if (!this.IsKeepChangeToAdvancePayment) this._billing.AdvancePayment.Amount = 0;
                    this.Success = true;

                    PaymentTransaction payTransaction = new(service);
                    AdvancePaymentTransaction advancePaymentTransaction = new(service);
                    BillingTransaction billingTransaction = new(service);
                    PaymentCheckTransaction paymentCheckTransaction = new(service);

                    PaymentCheck check = new();
                    check.Branch = this.Branch;
                    check.CheckNumber = this.CheckNumber;
                    check.CheckDate = this.CheckDate;
                    check.Amount = this.CheckAmount;

                    Task<PaymentBase> savePayment = payTransaction.SaveUpdate(user.Id, this);
                    Task<TMCWD.Model.Billing.AdvancePayment> updateAdvancePayment = advancePaymentTransaction.SaveUpdate(user.Id, this._billing.AdvancePayment);
                    Task<TMCWD.Model.Billing.Interfaces.BillingBase> updateBilling = billingTransaction.SaveUpdate(user.Id, this._billing);
                    Task<TMCWD.Model.Billing.PaymentCheck> saveCheck = paymentCheckTransaction.SaveUpdate(user.Id, check);

                    await Task.WhenAll(savePayment, updateAdvancePayment, updateBilling, saveCheck);
                    if (savePayment.Result.Id > 0 && updateAdvancePayment.Id > 0 && updateAdvancePayment.Id > 0 && saveCheck.Id > 0) isSuccess = true;

                }

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
