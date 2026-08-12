using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace TMCWD.Data.Entities
{

    [Table("readings")]
    public class Reading
    {

        #region constructors

        public Reading() { }

        #endregion

        #region properties

        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Required, Column("AccountId")]
        public System.Int64 AccountId { get; set; }

        [Required, Column("ReadingSheetId")]
        public System.Int64 ReadingSheetId { get; set; }

        [Required, Column("Reading")]
        public decimal CurrentReading { get; set; }

        [Column("PreviousReading")]
        public decimal PreviousReading { get; set; }

        [Column("Status")]
        public ReadingStatus Status { get; set; } = ReadingStatus.Created;

        [Column("IsCompleted")]
        public bool IsCompleted { get; set; }

        [Column("CreatedBy")]
        public System.Int64 CreatedBy { get; set; }

        [Column("DateCreated")]
        public DateTime DateCreated { get; set; }

        [Column("UpdatedBy")]
        public System.Int64 UpdatedBy { get; set; }

        [Column("DateUpdated")]
        public DateTime DateUpdated { get; set; }

        #endregion

    }
}
