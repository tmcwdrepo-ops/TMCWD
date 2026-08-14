using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.Billing.Requests
{
    public class SaveOtherChargeRequest
    {
        public string AccountNumber { get; set; } = string.Empty;
        public int ChargeType { get; set; }
        public string PayableIn { get; set; } = string.Empty;
        public string Particulars { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
