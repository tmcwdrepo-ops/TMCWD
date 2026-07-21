using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.CustomerSupport.Interfaces
{
    public interface IInspectionTypeDetail
    {

        public int Id { get; set; }

        public int InspectionTypeId { get; set; }

        public string Detail { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateUpdated { get; set; }

    }
}
