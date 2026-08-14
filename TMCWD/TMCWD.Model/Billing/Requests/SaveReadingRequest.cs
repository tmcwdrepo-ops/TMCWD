using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.Billing.Requests
{
   
        /// <summary>
        /// Request model for saving a reading from the UI.
        /// </summary>
        public class SaveReadingRequest
        {
            public string AccountNumber { get; set; } = string.Empty;
            public decimal PresentReading { get; set; }
            public decimal PreviousReading { get; set; }
            public int Zone { get; set; }
            public int Book { get; set; }
            public DateTime BillingDate { get; set; }
            public DateTime ReadingDate { get; set; }
            public int ReaderId { get; set; }
        }
    }


