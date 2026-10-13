using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TMCWD.Model.CustomerSupport;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CustomerWriteRequest
{
    [Range(0, int.MaxValue)] public int Id { get; set; }
    [Required, MaxLength(50)] public string Firstname { get; set; } = "";
    [MaxLength(50)] public string Middlename { get; set; } = "";
    [Required, MaxLength(50)] public string Lastname { get; set; } = "";
    [Required, MaxLength(11)] public string PhoneNumber { get; set; } = "";
    [Required, MaxLength(100)] public string Email { get; set; } = "";
    public bool IsActive { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AccountWriteRequest
{
    [Range(0, int.MaxValue)] public int Id { get; set; }
    [Range(1, int.MaxValue)] public int CustomerId { get; set; }
    [MaxLength(50)] public string MeterNumber { get; set; } = "";
    [Range(typeof(decimal), "0", "999999")] public decimal MeterSize { get; set; }
    [Range(0, 9)] public int Classification { get; set; }
    [MaxLength(100)] public string? UnitNumber { get; set; }
    [MaxLength(100)] public string? Building { get; set; }
    [Required, MaxLength(100)] public string HouseNumber { get; set; } = "";
    [MaxLength(100)] public string Street { get; set; } = "";
    [MaxLength(100)] public string Barangay { get; set; } = "";
    public bool IsCurrentAddress { get; set; }
}
