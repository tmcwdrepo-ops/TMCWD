using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using TMCWD.Model.Billing.Interfaces;

namespace TMCWD.Model.Billing
{
    public class Tariff : ITariff
    {

        #region members

        [DisplayName("Id")]
        public int Id { get; set; }

        [DisplayName("Classification")]
        public AccountClassification Classification { get; set; }

        [DisplayName("SizeInInches")]
        public decimal SizeInInches { get; set; }

        [DisplayName("MinimumReading")]
        public int MinimumReading { get; set; }

        [DisplayName("MaximumReading")]
        public int MaximumReading { get; set; }

        [DisplayName("ChargeAmount")]
        public decimal ChargeAmount { get; set; }

        [DisplayName("Interval")]
        public int Interval { get; set; }

        #endregion

    }
}
