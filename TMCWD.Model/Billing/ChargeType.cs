using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using TMCWD.Model.Billing.Interfaces;

namespace TMCWD.Model.Billing
{
    public class ChargeType : IChargeType
    {
        public ChargeType() { }

        [DisplayName("Id")]
        public int Id { get; set; }

        [DisplayName("Name")]
        public string Name { get; set; } = string.Empty;

        [DisplayName("Fee Classification")]
        public FeeClassification ClassificationId { get; set; }

        [DisplayName("Created By")]
        public int CreatedBy { get; set; }

        [DisplayName("Date Created")]
        public DateTime DateCreated { get; set; }

        [DisplayName("Updated By")]
        public int UpdatedBy { get; set; }

        [DisplayName("Date Updated")]
        public DateTime DateUpdated { get; set; }
    }
}
