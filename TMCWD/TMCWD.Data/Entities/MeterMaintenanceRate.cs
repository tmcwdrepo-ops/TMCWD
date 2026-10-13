using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities;

[Table("meter_maintenance_rates")]
public class MeterMaintenanceRate
{
    [Key, Column("Id")]
    public long Id { get; set; }

    [Required, Column("Amount")]
    public decimal Amount { get; set; }

    [Required, Column("EffectiveFrom")]
    public DateOnly EffectiveFrom { get; set; }

    [Column("EffectiveTo")]
    public DateOnly? EffectiveTo { get; set; }

    [Required, Column("CreatedBy")]
    public long CreatedBy { get; set; }

    [Required, Column("DateCreated")]
    public DateTime DateCreated { get; set; }

    [Required, Column("UpdatedBy")]
    public long UpdatedBy { get; set; }

    [Required, Column("DateUpdated")]
    public DateTime DateUpdated { get; set; }
}
