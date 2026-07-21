using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{
    [Table("inspection_type_details")]
    public class InspectionTypeDetail
    {

        #region constructors

        public InspectionTypeDetail() 
        {
            this.Detail = string.Empty;
        }

        #endregion

        #region properties

        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Required, Column("InspectionTypeId")]
        public System.Int64 InspectionTypeId { get; set; }

        [Required, MaxLength(255), Column("Detail")]
        public string Detail { get; set; }

        [Column("DateCreated")]
        public DateTime DateCreated { get; set; }

        [Column("DateUpdated")]
        public DateTime DateUpdated { get; set; }

        #endregion

    }
}
