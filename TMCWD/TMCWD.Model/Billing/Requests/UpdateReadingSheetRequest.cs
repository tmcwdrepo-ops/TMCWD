using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.Billing.Requests
{
    public class UpdateReadingSheetRequest
    {
        public int Id { get; set; }

        public int AssignedTo { get; set; }

        public DateTime BillingDate { get; set; }

        public int ZoneBookId { get; set; }

        public int Status { get; set; }
    }
}
