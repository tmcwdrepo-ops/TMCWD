using System;
namespace TMCWD.Model.Administrator.Interface
{
    public interface IUser
    {

        #region properties
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public int Role { get; set; }
        public DateTime DateVerified { get; set; }
        public string Password { get; set; }
        public string RememberToken { get; set; }
        public DateTime DateCreated { get; set; }
        public int CreatedBy { get; set; }
        public DateTime DateUpdated { get; set; }
        public int UpdatedBy { get; set; }
        public bool IsVerified { get; set; }
        public bool IsActive { get; set; }
        #endregion

    }
}
