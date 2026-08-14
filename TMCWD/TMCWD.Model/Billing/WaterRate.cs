using System;
using System.ComponentModel;

namespace TMCWD.Model.Billing
{
    public class WaterRate
    {
        [DisplayName("Id")]
        public int Id { get; set; }

        [DisplayName("Classification")]
        public AccountClassification Classification { get; set; }

        [DisplayName("MeterSize")]
        public decimal MeterSize { get; set; }

        [DisplayName("MinimumCharge")]
        public decimal MinimumCharge { get; set; }

        [DisplayName("Rate11To20")]
        public decimal Rate11To20 { get; set; }

        [DisplayName("Rate21To30")]
        public decimal Rate21To30 { get; set; }

        [DisplayName("Rate31To40")]
        public decimal Rate31To40 { get; set; }

        [DisplayName("Rate41Up")]
        public decimal Rate41Up { get; set; }

        [DisplayName("EffectiveDate")]
        public DateTime EffectiveDate { get; set; }

        [DisplayName("IsActive")]
        public bool IsActive { get; set; }

        [DisplayName("DateCreated")]
        public DateTime DateCreated { get; set; }

        [DisplayName("DateUpdated")]
        public DateTime DateUpdated { get; set; }
    }
}