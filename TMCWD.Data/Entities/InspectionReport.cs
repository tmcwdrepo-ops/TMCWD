using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{
    [Table("inspection_reports")]
    public class InspectionReport
    {
        [Key, Column("Id")]
        public System.Int64 Id { get; set; }
        [Required, Column("RequestId")]
        public System.Int64 RequestId { get; set; }
        [Required, Column("InspectedByUserId")]
        public System.Int64 InspectedByUserId { get; set; }
        [Required, MaxLength(255), Column("Details")]
        public string Details { get; set; } = string.Empty;
        [Column("DateCreated")]
        public DateTime DateCreated { get; set; }
        [Column("CreatedBy")]
        public System.Int64 CreatedBy { get; set; }
        [Column("DateUpdated")]
        public DateTime DateUpdated { get; set; }
        [Column("UpdatedBy")]
        public System.Int64 UpdatedBy { get; set; }
    }
}
