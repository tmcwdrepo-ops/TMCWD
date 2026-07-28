using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{
    [Table("reading_sheet_templates")]
    public class ReadingSheetTemplate
    {

        #region properties

        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Required, MaxLength(50), Column("Name")]
        public string Name { get; set; }

        [Required, Column("ReaderId")]
        public System.Int64 ReaderId { get; set; }

        [Required, Column("ZoneBookId")]
        public System.Int64 ZoneBookId { get; set; }

        [Required, Column("CreatedBy")]
        public System.Int64 CreatedBy { get; set; }

        [Column("DateCreated")]
        public DateTime DateCreated { get; set; }

        [Column("UpdatedBy")]
        public System.Int64 UpdatedBy { get; set; }

        [Column("DateUpdated")]
        public DateTime DateUpdated { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; } = true;

        #endregion

    }
}
