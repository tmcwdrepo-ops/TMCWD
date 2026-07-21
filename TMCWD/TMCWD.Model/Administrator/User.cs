using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using TMCWD.Model.Administrator.Interface;

namespace TMCWD.Model.Administrator
{
    public class User : IUser
    {
        #region constructor
        /// <summary>
        /// Initializes a new instance of the User class with default property values.
        /// </summary>
        public User()
        {
            this.Name = string.Empty;
            this.Email = string.Empty;
            this.Role = 0;
            this.Password = string.Empty;
            this.RememberToken = string.Empty;
        }
        #endregion

        #region properties

        [DisplayName("Id")]
        public int Id { get; set; }
        [DisplayName("Name")]
        public string Name { get; set; }
        [DisplayName("Email")]
        public string Email { get; set; }
        [DisplayName("Role")]
        public int Role { get; set; }
        [DisplayName("Date Verified")]
        public DateTime DateVerified { get; set; }
        [DisplayName("Password")]
        public string Password { get; set; }
        [DisplayName("Remember Token")]
        public string RememberToken { get; set; }
        [DisplayName("Created")]
        public DateTime DateCreated { get; set; }
        [DisplayName("Enrolled By")]
        public int CreatedBy { get; set; }
        [DisplayName("Updated")]
        public DateTime DateUpdated { get; set; }
        [DisplayName("Updated By")]
        public int UpdatedBy { get; set; }
        public bool IsVerified { get; set; }
        [DisplayName("Active")]
        public bool IsActive { get; set; }
        #endregion
    }
}
