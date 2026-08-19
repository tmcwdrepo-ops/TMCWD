using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.Billing.Responses
{
    public class ReadingSheetAccountDto
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public string Number { get; set; }
        public decimal Prev { get; set; }
        public decimal Pres { get; set; }
        public decimal Usage { get; set; }
        public string Trend { get; set; }
        public decimal Balance { get; set; }
        public decimal Amount { get; set; }
        public decimal Total { get; set; }
        public string Status { get; set; }
        public string Category { get; set; }
    }
}
