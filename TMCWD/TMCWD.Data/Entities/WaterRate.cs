using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{
    [Table("water_rates")]
    public class WaterRate
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Required]
        [Column("Classification")]
        public int Classification { get; set; }

        [Required]
        [Column("MeterSize")]
        public decimal MeterSize { get; set; }

        [Required]
        [Column("MinimumCharge")]
        public decimal MinimumCharge { get; set; }

        [Required]
        [Column("Rate11To20")]
        public decimal Rate11To20 { get; set; }

        [Required]
        [Column("Rate21To30")]
        public decimal Rate21To30 { get; set; }

        [Required]
        [Column("Rate31To40")]
        public decimal Rate31To40 { get; set; }

        [Required]
        [Column("Rate41Up")]
        public decimal Rate41Up { get; set; }

        [Required]
        [Column("EffectiveDate")]
        public DateTime EffectiveDate { get; set; }

        [Required]
        [Column("IsActive")]
        public bool IsActive { get; set; }

        [Required]
        [Column("DateCreated")]
        public DateTime DateCreated { get; set; }

        [Required]
        [Column("DateUpdated")]
        public DateTime DateUpdated { get; set; }
    }
}