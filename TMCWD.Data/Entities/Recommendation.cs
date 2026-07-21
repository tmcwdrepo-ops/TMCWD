using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{
    [Table("recommendations")]
    public class Recommendation
    {
        [Key, Column("Id")]
        public System.Int64 Id { get; set; }
        [Required, Column("RequestId")]
        public System.Int64 RequestId { get; set; }
        [Required, MaxLength(255), Column("Detail")]
        public string Details { get; set; } = string.Empty;
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
