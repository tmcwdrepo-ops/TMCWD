using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using TMCWD.Model.CustomerSupport.Interfaces;

namespace TMCWD.Model.CustomerSupport
{
    public class Customer : ICustomer
    {

        [DisplayName("Id")]
        public int Id { get; set; }
        [DisplayName("Firstname")]
        public string Firstname { get; set; } = string.Empty;
        [DisplayName("Lastname")]
        public string Lastname { get; set; } = string.Empty;
        [DisplayName("Middlename")]
        public string Middlename { get; set; } = string.Empty;
        [DisplayName("Full Name")]
        public string FullName => $"{Lastname}, {Firstname} {Middlename}";
        [DisplayName("Phone")]
        public string PhoneNumber { get; set; } = string.Empty;
        [DisplayName("Email Address")]
        public string Email { get; set; } = string.Empty;
        [DisplayName("Date Enrolled")]
        public DateTime DateCreated { get; set; }
        [DisplayName("Date Updated")]
        public DateTime DateUpdated { get; set; }
        [DisplayName("Active")]
        public bool IsActive { get; set; }
        [DisplayName("Enrolled By")]
        public int CreatedBy { get; set; }
        [DisplayName("Updated By")]
        public int UpdatedBy { get; set; }

    }
}
