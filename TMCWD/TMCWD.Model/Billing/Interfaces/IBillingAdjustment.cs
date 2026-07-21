using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.Billing.Interfaces
{
    internal interface IBillingAdjustment
    {

        public int Id { get; set; }

        public int Type { get; set; }

        public string BillingReferenceId { get; set; }

        public decimal Amount { get; set; }

        public int CreatedBy { get; set; }

        public DateTime DateCreated { get; set; }

        public int UpdatedBy { get; set; }

        public DateTime DateUpdated { get; set; }

    }
}
