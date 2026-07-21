using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.CustomerSupport.Interfaces
{
    public interface IJobOrder
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public int RequestDetailId { get; set; }
        public string JobOrderNumber { get; set; }
        public JobOrderStatus Status { get; set; }
        public int CreatedBy { get; set; }
        public DateTime DateCreated { get; set; }
        public int UpdatedBy { get; set; }
        public DateTime DateUpdated { get; set; }
    }
}
