using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{
    [Table("job_orders")]
    public class JobOrder
    {
        [Key, Column("Id")]
        public int Id { get; set; }
        [Required, Column("RequestId")]
        public int RequestId { get; set; }
        [Required, Column("RequestDetailId")]
        public int RequestDetailId { get; set; }
        [Required, MaxLength(50), Column("JobOrderNumber")]
        public string JobOrderNumber { get; set; } = string.Empty;
        [Column("HasCharges")]
        public bool HasCharges { get; set; }
        [Required, Column("Status")]
        public int Status { get; set; }
        [Required, Column("CreatedBy")]
        public int CreatedBy { get; set; }
        [Column("DateCreated")]
        public DateTime DateCreated { get; set; }
        [Column("UpdatedBy")]
        public int UpdatedBy { get; set; }
        [Column("DateUpdated")]
        public DateTime DateUpdated { get; set; }
        public DateTime? JobOrderDate { get; set; }
        public long? ReferredToUserId { get; set; }
        public DateTime? DateReferred { get; set; }
    }
}
