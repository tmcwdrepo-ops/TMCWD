using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.CustomerSupport.Interfaces
{
    public interface IRequestDetail
    {

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
