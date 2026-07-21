using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using TMCWD.Model.CustomerSupport.Interfaces;

namespace TMCWD.Model.CustomerSupport
{
    public class ApprovalHistory : IApprovalHistory
    {
        [DisplayName("Id")]
        public int Id { get; set; }
        [DisplayName("Job Order Id")]
        public int JobOrderId { get; set; }
        [DisplayName("Details")]
        public string Details { get; set; } = string.Empty;
        [DisplayName("Remarks")]
        public string Remarks { get; set; } = string.Empty;
        [DisplayName("Created By")]
        public int CreatedBy { get; set; }
        [DisplayName("Date Created")]
        public DateTime DateCreated { get; set; }

    }
}
