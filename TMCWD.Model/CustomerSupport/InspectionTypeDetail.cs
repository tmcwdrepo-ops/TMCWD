using System;
using System.Collections.Generic;
using System.Text;
using TMCWD.Model.CustomerSupport.Interfaces;

namespace TMCWD.Model.CustomerSupport
{
    public class InspectionTypeDetail : IInspectionTypeDetail
    {

        #region properties
        public int Id { get; set; }
        public int InspectionTypeId { get; set; }
        public string Detail { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateUpdated { get; set; }
        #endregion

        #region constructors

        public InspectionTypeDetail() 
        { 
            this.Detail = string.Empty;
        }

        #endregion
    }
}
