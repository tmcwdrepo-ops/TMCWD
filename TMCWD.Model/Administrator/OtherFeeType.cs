using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using TMCWD.Model.Administrator.Interface;

namespace TMCWD.Model.Administrator
{
    public class OtherFeeType : IOtherFeeType
    {
        public OtherFeeType() { }

        [DisplayName("Id")]
        public int Id { get; set; }
        [DisplayName("Name")]
        public string Name { get; set; } = string.Empty;

        [DisplayName("Default Value")]
        public decimal DefaultValue { get; set; }
        [DisplayName("Enrolled By")]
        public int CreatedBy { get; set; }
        [DisplayName("Date Created")]
        public DateTime DateCreated { get; set; }
        [DisplayName("Updated By")]
        public int UpdatedBy { get; set; }
        [DisplayName("Date Updated")]
        public DateTime DateUpdated { get; set; }
    }
}
