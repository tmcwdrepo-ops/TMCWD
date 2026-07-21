using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.CustomerSupport.Interfaces
{
    public interface IApprovalHistory
    {
        public int Id { get; set; }

        public int JobOrderId { get; set; }

        public string Details { get; set; }

        public string Remarks { get; set; }

        public int CreatedBy { get; set; }

        public DateTime DateCreated { get; set; }
    }
}
