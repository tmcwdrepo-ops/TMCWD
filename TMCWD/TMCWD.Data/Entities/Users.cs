using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{
    [Table("users")]
    public class User
    {
        #region constructor
        public User() { }
        #endregion

        #region properties
        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Required, MaxLength(100), Column("Name")]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(100), Column("Email")]
        public string Email { get; set; } = string.Empty;

        [Required, Column("Role")]
        public int Role { get; set; }

        [Column("DateVerified")]
        public DateTime DateVerified { get; set; }

        [Required, MaxLength(100), Column("Password")]
        public string Password { get; set; } = string.Empty;

        [Column("RememberToken"), MaxLength(100)]
        public string RememberToken { get; set; } = string.Empty;

        [Column("DateCreated")]
        public DateTime DateCreated { get; set; }

        [Column("CreatedBy")]
        public System.Int64 CreatedBy { get; set; }

        [Column("DateUpdated")]
        public DateTime DateUpdated { get; set; }

        [Column("UpdatedBy")]
        public System.Int64 UpdatedBy { get; set; }

        [Column("IsVerified")]
        public bool IsVerified { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }
        #endregion

    }
}
