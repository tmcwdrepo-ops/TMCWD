using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.Billing.Interfaces
{
    public interface IPaymentCheck
    {
        public int Id { get; set; }

        public string BillingReference { get; set; }

        public string Branch { get; set; }

        public string CheckNumber { get; set; }

        public decimal Amount { get; set; }

        public DateTime CheckDate { get; set; }

        public System.Int64 CreatedBy { get; set; }

        public DateTime DateCreated { get; set; }

        public System.Int64 UpdatedBy { get; set; }

        public DateTime DateUpdated { get; set; }
    }
}
