using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using TMCWD.Model.Billing.Interfaces;

namespace TMCWD.Model.Billing
{
    public class OtherCharge : IOtherCharge
    {

        public OtherCharge() { }

        [DisplayName("Id")]
        public int Id { get; set; }

        [DisplayName("Type")]
        public int Type { get; set; }

        [DisplayName("Billing Reference Id")]
        public string BillingReferenceId { get; set; } = string.Empty;

        [DisplayName("Amount")]
        public decimal Amount { get; set; }

        [DisplayName("Payment Status")]
        public PaymentStatus PaymentStatus { get; set; }

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
