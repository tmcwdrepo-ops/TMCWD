using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{
    [Table("tariffs")]
    public class Tariff
    {

        #region members

        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Required, Column("Classification")]
        public int Classification { get; set; }

        [Required, Column("SizeInInches")]
        public decimal SizeInInches { get; set; }

        [Required, Column("MinimumReading")]
        public int MinimumReading { get; set; }

        [Required, Column("MaximumReading")]
        public int MaximumReading { get; set; }

        [Required, Column("ChargeAmount")]
        public int ChargeAmount { get; set; }

        [Required, Column("Interval")]
        public int Interval { get; set; }


        #endregion

    }
}
