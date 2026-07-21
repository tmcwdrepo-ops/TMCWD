using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using TMCWD.Model.CustomerSupport.Interfaces;

namespace TMCWD.Model.CustomerSupport
{
    public class Account : IAccount
    {
        [DisplayName("Id")]
        public int Id { get; set; }
        [DisplayName("Customer Id")]
        public int CustomerId { get; set; }
        [DisplayName("Account Number")]
        public string AccountNumber { get; set; } = string.Empty;

        [DisplayName("Zone Book Id")]
        public int ZoneBookId { get; set; }

        [DisplayName("Classification")]
        public AccountClassification Classification { get; set; }

        [DisplayName("Meter Size")]
        public decimal MeterSize { get; set; }

        [DisplayName("Unit No")]
        public string UnitNumber { get; set; } = string.Empty;

        [DisplayName("Building / Apartment")]
        public string Building { get; set; } = string.Empty;

        [DisplayName("House No")]
        public string HouseNumber { get; set; } = string.Empty;

        [DisplayName("Street")]
        public string Street { get;set;  } = string.Empty;

        [DisplayName("Barangay")]
        public string Barangay { get; set; } = string.Empty;

        [DisplayName("Meter Number")]
        public string MeterNumber { get; set; } = string.Empty;

        [DisplayName("Address")]
        public string FullAddress => $"{UnitNumber} {Building} {HouseNumber} {Street} {Barangay}";

        [DisplayName("Current")]
        public bool IsCurrentAddress { get; set; }

        [DisplayName("Date Enrolled")]
        public DateTime DateCreated { get; set; }

        [DisplayName("Date Updated")]
        public DateTime DateUpdated { get; set; }

        [DisplayName("Status")]
        public AccountStatus Status { get; set; }

        [DisplayName("Created By")]
        public int CreatedBy { get; set; }

        [DisplayName("Updated By")]
        public int UpdatedBy { get; set; }
    }
}
