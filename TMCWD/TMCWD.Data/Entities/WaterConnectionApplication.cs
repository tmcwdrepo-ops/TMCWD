using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities;

[Table("water_connection_applications")]
public class WaterConnectionApplication
{
    [Key, Column("id")]
    public long Id { get; set; }

    [Column("tracking_number")]
    public string TrackingNumber { get; set; } = string.Empty;

    [Column("full_name")]
    public string FullName { get; set; } = string.Empty;

    [Column("address")]
    public string Address { get; set; } = string.Empty;

    [Column("barangay")]
    public string Barangay { get; set; } = string.Empty;

    [Column("unit_number")]
    public string? UnitNumber { get; set; }

    [Column("building")]
    public string? Building { get; set; }

    [Column("house_number")]
    public string? HouseNumber { get; set; }

    [Column("street")]
    public string? Street { get; set; }

    [Column("contact_number")]
    public string ContactNumber { get; set; } = string.Empty;

    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Column("connection_type")]
    public string ConnectionType { get; set; } = string.Empty;

    [Column("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// One of: application, inspection, charging, payment,
    ///         installation, connect, tapping, done, rejected
    /// </summary>
    [Column("stage")]
    public string Stage { get; set; } = string.Empty;

    /// <summary>Generated account number once the application reaches 'done'.</summary>
    [Column("account_number")]
    public string? AccountNumber { get; set; }

    /// <summary>FK to customers.Id — set when the account is activated.</summary>
    [Column("customer_id")]
    public long? CustomerId { get; set; }

    /// <summary>FK to accounts.Id — set when the account is activated.</summary>
    [Column("account_id")]
    public long? AccountId { get; set; }

    [Column("inspection_data")]
    public string? InspectionData { get; set; }

    [Column("charging_data")]
    public string? ChargingData { get; set; }

    [Column("payment_data")]
    public string? PaymentData { get; set; }

    [Column("installation_data")]
    public string? InstallationData { get; set; }

    [Column("connect_data")]
    public string? ConnectData { get; set; }

    [Column("tapping_data")]
    public string? TappingData { get; set; }

    [Column("done_data")]
    public string? DoneData { get; set; }

    [Column("remarks")]
    public string? Remarks { get; set; }

    [Column("assigned_to")]
    public long? AssignedTo { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
