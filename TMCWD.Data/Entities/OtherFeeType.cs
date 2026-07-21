using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{

    [Table("other_fee_types")]
    public class OtherFeeType
    {

        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Required, MaxLength(50), Column("Name")]
        public string Name { get; set; } = string.Empty;

        [Column("DefaultValue")]
        public decimal DefaultValue { get; set; }

        [Required, Column("CreatedBy")]
        public System.Int64 CreatedBy { get; set; }

        [Column("DateCreated")]
        public DateTime DateCreated { get; set; } = DateTime.Now;

        [Column("UpdatedBy")]
        public System.Int64 UpdatedBy { get; set; }

        [Column("DateUpdated")]
        public DateTime DateUpdate { get; set; } = DateTime.Now;

    }
}
