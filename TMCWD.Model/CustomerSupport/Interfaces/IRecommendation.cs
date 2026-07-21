using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.CustomerSupport.Interfaces
{
    public interface IRecommendation
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public string Details { get; set; }
        public int CreatedBy { get; set; }
        public DateTime DateCreated { get; set; }
        public int UpdatedBy { get; set; }
        public DateTime DateUpdated { get; set; }
    }
}
