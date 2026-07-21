using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{
    [Table("charge_types")]
    public class ChargeType
    {

        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Required, Column("Name")]
        public string Name { get; set; } = string.Empty;

        [Required, Column("ClassificationId")]
        public int ClassificationId { get; set; }

        [Column("CreatedBy")]
        public System.Int64 CreatedBy { get; set; }

        [Column("DateCreated")]
        public DateTime DateCreated { get; set; }

        [Column("UpdatedBy")]
        public System.Int64 UpdatedBy { get; set; }


        [Column("DateUpdated")]
        public DateTime DateUpdated { get; set; }

    }
}
