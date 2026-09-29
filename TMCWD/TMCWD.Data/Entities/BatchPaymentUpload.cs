using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{
    [Table("batch_payment_uploads")]
    public class BatchPaymentUpload
    {
        [Key, Column("Id")]
        public long Id { get; set; }

        [Required, MaxLength(260), Column("Filename")]
        public string Filename { get; set; } = string.Empty;

        [MaxLength(100), Column("Channel")]
        public string Channel { get; set; } = string.Empty;

        [MaxLength(50), Column("Status")]
        public string Status { get; set; } = "Processed";

        [Column("RowCount")]
        public int RowCount { get; set; }

        [Required, Column("CreatedBy")]
        public long CreatedBy { get; set; }

        [Column("DateCreated")]
        public DateTime DateCreated { get; set; }
    }
}
