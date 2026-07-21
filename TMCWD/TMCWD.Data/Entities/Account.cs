using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{

    [Table("accounts")]
    public class Account
    {

        #region constructors

        public Account() { }

        #endregion

        #region properties

        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Required, Column("CustomerId")]
        public System.Int64 CustomerId { get; set; }

        [Required, MaxLength(50), Column("AccountNumber")]
        public string AccountNumber { get; set; } = string.Empty;

        [Required, Column("ZoneBookId")]
        public int ZoneBookId { get; set; }

        [Required, Column("Classification")]
        public int Classification { get; set; }

        [Required, Column("MeterSize")]
        public decimal MeterSize { get; set; }

        [MaxLength(50), Column("MeterNumber")]
        public string MeterNumber { get; set; } = string.Empty;

        [Column("UnitNo")]
        public string? UnitNumber { get; set; } = string.Empty;

        [Column("Building")]
        public string? Building { get; set; } = string.Empty;

        [Column("HouseNo")]
        public string HouseNumber { get; set; } = string.Empty;

        [Column("Street")]
        public string Street { get; set; } = string.Empty;

        [Column("Barangay")]
        public string Barangay { get; set; } = string.Empty;

        [Required, Column("IsCurrentAddress")]
        public bool IsCurrentAddress { get; set; }

        [Required, Column("Status")]
        public int Status { get; set; }

        [Column("DateCreated")]
        public DateTime DateCreated { get; set; }

        [Column("DateUpdated")]
        public DateTime DateUpdated { get; set; }

        [Required, Column("CreatedBy")]
        public System.Int64 CreatedBy { get; set; }

        [Column("UpdatedBy")]
        public System.Int64 UpdatedBy { get; set; }
        #endregion

    }
}
