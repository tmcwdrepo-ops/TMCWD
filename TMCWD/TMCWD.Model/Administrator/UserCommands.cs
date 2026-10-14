using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TMCWD.Model.Administrator;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UserWriteRequest
{
    [Range(0, int.MaxValue)] public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(100)] public string Email { get; set; } = string.Empty;
    [Range((int)UserRole.SuperAdmin, (int)UserRole.Accounting)] public int? Role { get; set; }
    public bool? IsActive { get; set; }
    [MaxLength(256)] public string? Password { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PasswordChangeRequest
{
    [Required, MaxLength(256)] public string CurrentPassword { get; set; } = string.Empty;
    [Required, MaxLength(256)] public string NewPassword { get; set; } = string.Empty;
    [Required, MaxLength(256)] public string ConfirmPassword { get; set; } = string.Empty;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PasswordLoginRequest
{
    [Required, MaxLength(100)] public string Identifier { get; set; } = string.Empty;
    [Required, MaxLength(256)] public string Password { get; set; } = string.Empty;
}

public sealed record UserProfileDto(long Id, string? Name, string? Email, int? Role,
    bool? IsActive, bool? IsVerified, DateTime? DateCreated, DateTime? DateUpdated);
