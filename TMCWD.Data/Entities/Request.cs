using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{

    [Table("requests")]
    public class Request
    {

        #region constructors

        public Request() { }

        #endregion

        #region properties

        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Required, MaxLength(50), Column("ControlNumber")]
        public string ControlNumber { get; set; } = string.Empty;

        [Column("CustomerId")]
        public System.Int64 CustomerId { get; set; }

        [Column("AccountId")]
        public System.Int64 AccountId { get; set; }

        [Column("Status")]
        public int Status { get; set; }

        [Required, Column("CreatedBy")]
        public System.Int64 CreatedBy { get; set; }

        [Column("DateCreated")]
        public DateTime DateCreated { get; set; }

        [Column("UpdatedBy")]
        public System.Int64 UpdatedBy { get; set;  }

        [Column("DateUpdated")]
        public DateTime DateUpdated { get; set; }

        #endregion

    }
}
