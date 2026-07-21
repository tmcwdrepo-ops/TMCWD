using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.CustomerSupport.Interfaces
{
    public interface IAccount
    {

        #region properties

        public int Id { get; set; }

        public int CustomerId { get; set; }

        public string AccountNumber { get; set; }

        public AccountClassification Classification { get; set; }

        public decimal MeterSize { get; set; }

        public string MeterNumber { get; set; }

        public string UnitNumber { get; set; }

        public string Building { get; set; }

        public string HouseNumber { get; set; }

        public string Street { get; set; }

        public string Barangay { get; set; }

        public bool IsCurrentAddress { get; set; }
        
        public DateTime DateCreated { get; set; }

        public DateTime DateUpdated { get; set; }

        public AccountStatus Status { get; set; }

        public int CreatedBy { get; set; }

        public int UpdatedBy { get; set; }

        #endregion

    }
}
