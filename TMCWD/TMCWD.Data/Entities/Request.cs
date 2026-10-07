using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{

    [Table("requests")]
    public class Request
    {

        #region constructors

        public Request() { }

        #endregion

        #region properties

        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Required, MaxLength(50), Column("ControlNumber")]
        public string ControlNumber { get; set; } = string.Empty;

        [Column("CustomerId")]
        public System.Int64? CustomerId { get; set; }

        [Column("AccountId")]
        public System.Int64? AccountId { get; set; }

        [Column("Status")]
        public int Status { get; set; }

        [Required, Column("CreatedBy")]
        public System.Int64 CreatedBy { get; set; }

        [Column("DateCreated")]
        public DateTime DateCreated { get; set; }

        [Column("UpdatedBy")]
        public System.Int64? UpdatedBy { get; set;  }

        [Column("DateUpdated")]
        public DateTime? DateUpdated { get; set; }

        public bool IsServiceRequest { get; set; }
        public bool IsNonCustomer { get; set; }
        public long? LegacyServiceRequestId { get; set; }
        [MaxLength(50)] public string AccountNumber { get; set; } = string.Empty;
        [MaxLength(200)] public string AccountName { get; set; } = string.Empty;
        [MaxLength(500)] public string Address { get; set; } = string.Empty;
        [MaxLength(200)] public string ReportedBy { get; set; } = string.Empty;
        [MaxLength(50)] public string ContactNumber { get; set; } = string.Empty;
        [MaxLength(100)] public string Source { get; set; } = string.Empty;
        [MaxLength(200)] public string Name { get; set; } = string.Empty;
        public string? Particulars { get; set; }

        #endregion

    }
}
