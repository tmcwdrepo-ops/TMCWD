using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.Billing.Requests
{
    public class SaveReadingSheetRequest
    {
        public int Zone { get; set; }
        public int Book { get; set; }
        public int AssignedTo { get; set; }
        public DateTime BillingPeriod { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? DisconnectionDate { get; set; }
        public DateTime? BillingPeriodStart { get; set; }
        public int? SeqFrom { get; set; }
        public int? SeqTo { get; set; }
    }
}
