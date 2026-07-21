using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{

    [Table("files")]
    public class RequestFile
    {
        public RequestFile() { }

        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Required, Column("JobOrderId")]
        public System.Int64 JobOrderId { get; set; }

        [Required, Column("RequestFileType")]
        public int RequestFileType { get; set; }

        [Required, Column("Path")]
        public string Path { get; set; } = string.Empty;

        [Required, Column("OriginalFilename")]
        public string OriginalFilename { get; set; } = string.Empty;

        [Required, Column("PhysicalFilename")]
        public string PhysicalFilename { get; set; } = string.Empty;

        [Required, Column("FileType")]
        public int FileType { get; set; }

        [Required, Column("Size")]
        public long Size { get; set; }

        [Column("CreatedBy")]
        public System.Int64 CreatedBy { get; set; }

        [Column("DateCreated")]
        public DateTime DateCreated { get; set; }

        [Column("UpdatedBy")]
        public System.Int64 UpdatedBy { get; set; }

        [Column("DateUpdated")]
        public DateTime DateUpdate { get; set; }

    }
}
