using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace TMCWD.Model.Billing.Interfaces
{
    public abstract class BillingBase
    {

        #region fields

        private AdvancePayment _advancePayment;
        private decimal _advancePaymentAmount;
        private decimal _unpaid;
        private decimal _totalPaid;

        #endregion

        #region constructors

        public BillingBase()
        {
            _unpaid = 0;
            _totalPaid = 0;
            _advancePayment = new();
        }

        #endregion

        #region properties

        [DisplayName("Id")]
        public int Id { get; set; }

        [DisplayName("Account Id")]
        public int AccountId { get; set; }

        [DisplayName("Job Order Id")]
        public int JobOrderId { get; set; }

        [DisplayName("Billing Reference")]
        public string BillingReferenceId { get; set; } = string.Empty;

        [DisplayName("Payment Transaction Id")]
        public string PaymentTransactionId { get; set; } = string.Empty;

        [DisplayName("MaterialsAmount")]
        public decimal MaterialsAmount { get; set; }

        [DisplayName("Penalties")]
        public decimal Penalties { get; set; }

        [DisplayName("Other Charges")]
        public decimal OtherCharges { get; set; }

        [DisplayName("Billing Adjustment")]
        public decimal BillingAdjustment { get; set; }

        [DisplayName("Total Bill Amount")]
        public decimal TotalBillAmount { get; set; }

        [DisplayName("Status")]
        public PaymentStatus PaymentStatus { get; set; }

        [DisplayName("Created By")]
        public int CreatedBy { get; set; }

        [DisplayName("Date Created")]
        public DateTime DateCreated { get; set; }

        [DisplayName("Updated By")]
        public int UpdatedBy { get; set; }

        [DisplayName("Date Updated")]
        public DateTime DateUpdated { get; set; }

        #endregion

        #region methods

        public abstract void LoadPayments(List<PaymentBase> payments, AdvancePayment advancePayment);

        public abstract decimal ComputeBill(Reading previousReading, Reading currentReading, List<Penalty> penalties, List<BillingAdjustment> adjustments, List<Tariff> tariffs);

        #endregion

    }
}
