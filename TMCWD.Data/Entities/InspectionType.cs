using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{
    [Table("inspection_types")]
    public class InspectionType
    {

        #region constructors

        public InspectionType() { }

        #endregion

        #region properties

        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Required, MaxLength(150), Column("Name")]
        public string Name { get; set; } = string.Empty;

        [Required, Column("WithDetail")]
        public bool WithDetail { get; set; }

        [Column("IsRequiredAccount")]
        public bool IsRequiredAccount { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }

        [Column("IsNew")]
        public bool IsNew { get; set; }

        [Column("DateCreated")]
        public DateTime DateCreated { get; set; }

        [Required, Column("CreatedBy")]
        public System.Int64 CreatedBy { get; set; }

        [Column("DateUpdated")]
        public DateTime DateUpdated { get; set; }

        [Column("UpdatedBy")]
        public System.Int64 UpdatedBy { get; set; }

        #endregion
    }
}
