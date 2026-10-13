using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using TMCWD.Data.Context;
using TMCWD.Utility.Encryption;

namespace TMCWD.Data.Services;

public static class PersistedTokenValidation
{
    public static async Task Validate(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var identities = principal?.FindAll(ClaimTypes.NameIdentifier).ToArray() ?? [];
        var roles = principal?.FindAll(ClaimTypes.Role).ToArray() ?? [];
        var stamps = principal?.FindAll(UserSecurityStamp.ClaimType).ToArray() ?? [];
        if (identities.Length != 1 || roles.Length != 1 || stamps.Length != 1
            || !int.TryParse(identities[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0
            || !int.TryParse(roles[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var role)
            || !Enum.IsDefined(typeof(UserRole), role)
            || context.Options.TokenValidationParameters.IssuerSigningKey is not SymmetricSecurityKey key)
        {
            context.Fail("Authentication is no longer valid.");
            return;
        }
        // No cache: each request sees committed security changes, independent of token/session lifetime.
        var db = context.HttpContext.RequestServices.GetRequiredService<UserDbContext>();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id,
            context.HttpContext.RequestAborted);
        if (user == null || user.IsActive != true || user.IsVerified != true || user.Role != role
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stamps[0].Value),
                Encoding.UTF8.GetBytes(UserSecurityStamp.Create(key.Key, user.Id, user.Password, user.Role, user.IsActive, user.IsVerified, user.DateUpdated, user.DateVerified))))
            context.Fail("Authentication is no longer valid.");
    }
}
