using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TMCWD.Utility.Encryption;

public static class UserSecurityStamp
{
    public const string ClaimType = "tmcwd_security_stamp";

    // Keyed and domain-separated: never expose the stored password hash as a token claim.
    public static string Create(byte[] key, long id, string? password, int? role, bool? active, bool? verified, DateTime? updatedAt, DateTime? verifiedAt)
    {
        var state = string.Join("\n", "TMCWD.SecurityStamp.v1", id.ToString(CultureInfo.InvariantCulture),
            password?.ToUpperInvariant(), role?.ToString(CultureInfo.InvariantCulture), active?.ToString(), verified?.ToString(),
            updatedAt?.ToString("yyyyMMddHHmmss.ffffff", CultureInfo.InvariantCulture),
            verifiedAt?.ToString("yyyyMMddHHmmss.ffffff", CultureInfo.InvariantCulture));
        return Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(state)));
    }
}
