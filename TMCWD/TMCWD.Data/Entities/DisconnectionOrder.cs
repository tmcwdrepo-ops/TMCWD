using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities;

[Table("disconnection_orders")]
public class DisconnectionOrder
{
    [Key, Column("id")]
    public long Id { get; set; }
    [Column("account_id")]
    public long AccountId { get; set; }
    [Column("job_order_number")]
    public string JobOrderNumber { get; set; } = string.Empty;
    [Column("job_order_date")]
    public DateTime JobOrderDate { get; set; }
    [Column("disconnection_date")]
    public DateTime? DisconnectionDate { get; set; }
    [Column("nature_of_disconnection")]
    public string NatureOfDisconnection { get; set; } = string.Empty;
    [Column("possession")]
    public string Possession { get; set; } = string.Empty;
    [Column("reason")]
    public string Reason { get; set; } = string.Empty;
    [Column("last_reading")]
    public decimal? LastReading { get; set; }
    [Column("plumber")]
    public string Plumber { get; set; } = string.Empty;
    [Column("reconnection_fee")]
    public decimal ReconnectionFee { get; set; }
    [Column("remarks")]
    public string Remarks { get; set; } = string.Empty;
    [Column("charge_fee")]
    public bool ChargeFee { get; set; }
    [Column("reading_override")]
    public bool ReadingOverride { get; set; }
    [Column("status")]
    public string Status { get; set; } = string.Empty;
    [Column("processed_by")]
    public long ProcessedBy { get; set; }
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
