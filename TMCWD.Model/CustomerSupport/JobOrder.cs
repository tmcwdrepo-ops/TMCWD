using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using TMCWD.Model.CustomerSupport.Interfaces;

namespace TMCWD.Model.CustomerSupport
{
    public class JobOrder : IJobOrder
    {
        public JobOrder() { }

        [DisplayName("Id")]
        public int Id { get; set; }
        [DisplayName("Request Id")]
        public int RequestId { get; set; }
        [DisplayName("Request Detail Id")]
        public int RequestDetailId { get; set; }
        [DisplayName("Job Order Number")]
        public string JobOrderNumber { get; set; } = string.Empty;
        [DisplayName("Has Charges")]
        public bool HasCharges { get; set; }
        [DisplayName("Status")]
        public JobOrderStatus Status { get; set; }
        [DisplayName("Enrolled By")]
        public int CreatedBy { get; set; }
        [DisplayName("Date Enrolled")]
        public DateTime DateCreated { get; set; }
        [DisplayName("Updated By")]
        public int UpdatedBy { get; set; }
        [DisplayName("Date Updated")]
        public DateTime DateUpdated { get; set; }
    }
}
