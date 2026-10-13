using System;
using System.ComponentModel;
using TMCWD.Model.Billing.Interfaces;

namespace TMCWD.Model.Billing
{
    public class BillingAdjustment : IBillingAdjustment
    {
        public BillingAdjustment() { }

        [DisplayName("Id")]
        public int Id { get; set; }

        [DisplayName("Type")]
        public int Type { get; set; }

        [DisplayName("Billing Reference Id")]
        public string BillingReferenceId { get; set; } = string.Empty;

        [DisplayName("Amount")]
        public decimal Amount { get; set; }

        /// <summary>
        /// Optional free-text reason. NULL for historical records.
        /// </summary>
        [DisplayName("Reason")]
        public string? Reason { get; set; }

        [DisplayName("Created By")]
        public int CreatedBy { get; set; }

        [DisplayName("Date Created")]
        public DateTime DateCreated { get; set; }

        [DisplayName("Updated By")]
        public int UpdatedBy { get; set; }

        [DisplayName("Date Updated")]
        public DateTime DateUpdated { get; set; }
    }
}
