using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{
    [Table("workflows")]
    public class Workflow
    {
        public Workflow() { }

        [Key, Column("Id")]
        public int Id { get; set; }

        [Required, MaxLength(50), Column("Name")]
        public string Name { get; set; } = string.Empty;

        [Column("Sequence")]
        public int Sequence { get; set; }

        [Column("Predecessor")]
        public string Predecessor { get; set; } = string.Empty;

        [Required, Column("CreatedBy")]
        public int CreatedBy { get; set; }

        [Column("DateCreated")]
        public DateTime DateCreated { get; set; } = DateTime.Now;

        [Column("UpdatedBy")]
        public int UpdatedBy { get; set; }

        [Column("DateUpdated")]
        public DateTime DateUpdated { get; set; }
    }
}
