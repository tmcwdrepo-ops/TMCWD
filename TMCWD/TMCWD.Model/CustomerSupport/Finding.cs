using System;
using System.Collections.Generic;
using System.Text;
using TMCWD.Model.CustomerSupport.Interfaces;

namespace TMCWD.Model.CustomerSupport
{
    public class Finding : IFinding
    {
        public Finding() { }
        public int Id { get; set; }
        public int JobOrderId { get; set; }
        public string Detail { get; set; } = string.Empty;
        public int CreatedBy { get; set; }
        public DateTime DateCreated { get; set; }
        public int UpdatedBy { get; set; }
        public DateTime DateUpdated { get; set; }
    }
}
