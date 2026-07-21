using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.CustomerSupport.Interfaces
{
    public interface ICustomer
    {

        #region properties

        public int Id { get; set; }

        public string Firstname { get; set; }

        public string Lastname { get; set; }

        public string Middlename { get; set; }

        public string Fullname
        {
            get
            {
                return $"{Firstname} {Middlename} {Lastname}";
            }
        }

        public string PhoneNumber { get; set; }

        public string Email { get; set; }

        public DateTime DateCreated { get; set; }

        public DateTime DateUpdated { get; set; }

        public bool IsActive { get; set; }

        #endregion

    }
}
