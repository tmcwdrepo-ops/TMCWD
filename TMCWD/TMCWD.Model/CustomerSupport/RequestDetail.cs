using System;
using System.Collections.Generic;
using System.Text;
using TMCWD.Model.CustomerSupport.Interfaces;

namespace TMCWD.Model.CustomerSupport
{
    public class RequestDetail : IRequestDetail
    {

        #region constructors

        public RequestDetail() 
        {
            this.InspectionTypeDetail = string.Empty;
        }

        #endregion

        #region properties

        public int Id { get; set; }
        public int RequestId { get; set; }
        public int RequestTypeId { get; set; }
        public string InspectionTypeDetail { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateUpdated { get; set; }

        #endregion
    }
}
