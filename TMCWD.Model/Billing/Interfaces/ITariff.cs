using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.Billing.Interfaces
{
    public interface ITariff
    {

        public int Id { get; set; }

        public AccountClassification Classification { get; set; }

        public decimal SizeInInches { get; set; }

        public int MinimumReading { get; set; }

        public int MaximumReading { get; set; }

        public decimal ChargeAmount { get; set; }

        public int Interval { get; set; }

    }
}
