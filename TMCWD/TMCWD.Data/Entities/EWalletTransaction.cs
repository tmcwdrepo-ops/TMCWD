using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{
    [Table("ewallet_transaction")]
    public class EWalletTransaction
    {
        #region constructors

        public EWalletTransaction() { }

        #endregion

        #region properties

        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Required, Column("PaymentReference")]
        public string PaymentReference { get; set; } = string.Empty;

        [Required, Column("GatewayType")]
        public int GatewayType { get; set; }

        [Required, Column("Data")]
        public string Data { get; set; } = string.Empty;

        [Required, Column("CreatedBy")]
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
