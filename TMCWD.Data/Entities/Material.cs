using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace TMCWD.Data.Entities
{

    [Table("materials")]
    public class Material
    {

        #region constructors

        public Material() { }

        #endregion

        #region members

        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Required, Column("InventoryId")]
        public System.Int64 InventoryId { get; set; }

        [Required, Column("JobOrderId")]
        public System.Int64 JobOrderId { get; set; }

        [Required, Column("RequestedQuantity")]
        public System.Int64 RequestedQuantity { get; set; } = 0;

        [Required, Column("UnitSellingPrice")]
        public decimal UnitSellingPrice { get; set; }

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
