using System;
using System.ComponentModel;
using System.Text;

namespace TMCWD.Model.Billing.Interfaces
{
    public interface IPenalty
    {
        public int Id { get; set; }

        public string BillingReferenceId { get; set; }

        public decimal Amount { get; set; }

        public int PenaltyTypeId { get; set; }

        public PaymentStatus PaymentStatus { get; set; }

        public int CreatedBy { get; set; }

        public DateTime DateCreated { get; set; }

        public int UpdatedBy { get; set; }

        public DateTime DateUpdated { get; set; }
    }
}
