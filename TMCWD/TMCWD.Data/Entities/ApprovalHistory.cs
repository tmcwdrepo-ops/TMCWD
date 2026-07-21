using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{
    [Table("approval_history")]
    public class ApprovalHistory
    {
        public ApprovalHistory() { }

        [Required, Column("Id")]
        public System.Int64 Id { get; set; }

        [Required, Column("JobOrderId")]
        public System.Int64 JobOrderId { get; set; }

        [Required, MaxLength(255), Column("Details")]
        public string Details { get; set; } = string.Empty;

        [Required, MaxLength(255), Column("Remarks")]
        public string Remarks { get; set; } = string.Empty;

        [Required, Column("Status")]
        public System.Int64 Status { get; set; }

        [Column("CreatedBy")]
        public System.Int64 CreatedBy { get; set; }

        [Column("DateCreated")]
        public DateTime DateCreated { get; set; }

    }
}
