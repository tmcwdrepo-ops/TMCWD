using System;
using System.Text;

namespace TMCWD.Model.Billing.Interfaces
{
    public interface IOtherCharge
    {

        public int Id { get; set; }

        public int Type { get; set; }

        public string BillingReferenceId { get; set; }

        public decimal Amount { get; set; }

        public PaymentStatus PaymentStatus { get; set; }

        public int CreatedBy { get; set; }

        public DateTime DateCreated { get; set; }

        public int UpdatedBy { get; set; }

        public DateTime DateUpdated { get; set; }

    }
}
