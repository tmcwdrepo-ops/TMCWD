using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.Billing.Interfaces
{
    public interface IeWalletTransaction
    {
        public int Id { get; set; }

        public string PaymentReference { get; set; }

        public GatewayType Type { get; set; } 

        public string Data { get; set; }

        public int CreatedBy { get; set;  }

        public DateTime DateCreated { get; set; }

        public int UpdatedBy { get; set; }

        public DateTime DateUpdated { get; set; }

    }
}
