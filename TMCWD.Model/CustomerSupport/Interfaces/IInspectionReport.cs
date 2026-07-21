using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.CustomerSupport.Interfaces
{
    public interface IInspectionReport
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public int InspectedByUserId { get; set; }
        public string Details { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateUpdated { get; set; }
    }
}
