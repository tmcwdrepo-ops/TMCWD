using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using TMCWD.Model.Administrator.Interface;

namespace TMCWD.Model.Administrator
{
    public class InspectionType : IInspectionType
    {

        [DisplayName("Id")]
        public int Id { get; set; }
        [DisplayName("Name")]
        public string Name { get; set; } = string.Empty;
        [DisplayName("With Detail")]
        public bool WithDetail { get; set; }
        [DisplayName("Required Account")]
        public bool IsRequiredAccount { get; set; }
        [DisplayName("Active")]
        public bool IsActive { get; set; }
        [DisplayName("Date Enrolled")]
        public DateTime DateCreated { get; set; }
        [DisplayName("Enrolled By")]
        public int CreatedBy { get; set; }
        [DisplayName("Date Updated")]
        public DateTime DateUpdated { get; set; }
        [DisplayName("Updated By")]
        public int UpdatedBy { get; set; }
    }
}
