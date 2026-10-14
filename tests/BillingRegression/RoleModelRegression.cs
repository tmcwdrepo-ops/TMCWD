using System.Globalization;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TMCWD.Administration;
using TMCWD.Services;
using TMCWD.Utility.Encryption;

static class RoleModelRegression
{
    public static async Task Run()
    {
        var names = new[] { "SuperAdmin", "CustomerRepresentative", "Engineer", "MeterReader", "Guest",
            "CustomerServiceOic", "Billing", "BillingHead", "GeneralServices", "Accounting" };
        var labels = new[] { "Super Admin", "Customer Representative", "Engineer", "Meter Reader", "Guest",
            "Customer Service OIC", "Billing", "Billing Head", "General Services", "Accounting" };
        var roles = Enum.GetValues<UserRole>();
        Check(roles.Length == names.Length && roles.Distinct().Count() == names.Length, "Role aliases or unexpected values.");
        using var services = new ServiceCollection().AddLogging().AddHttpClient()
            .AddAuthorization(TMCWD.Data.AuthorizationPolicies.Configure).BuildServiceProvider();
        var options = new UserTransaction(new WebService(services.GetRequiredService<IHttpClientFactory>())).GetRoles();
        Check(options.Count == names.Length, "Incomplete role-management choices.");
        for (var i = 0; i < names.Length; i++)
        {
            var value = i + 1;
            var role = (UserRole)value;
            Check(role.ToString() == names[i] && Enum.Parse<UserRole>(names[i]) == role, "Persisted role mapping changed.");
            Check(JsonSerializer.Deserialize<UserRole>(JsonSerializer.Serialize(role)) == role,
                "Role JSON round-trip failed.");
            var user = new TMCWD.Model.Administrator.User { Id = 1, Role = value };
            Check(JsonSerializer.Deserialize<TMCWD.Model.Administrator.User>(JsonSerializer.Serialize(user))?.Role == value,
                "Numeric session role round-trip failed.");
            Check(options.Single(o => o.Value == value.ToString(CultureInfo.InvariantCulture)).Text == labels[i],
                "Incorrect role-management label/value.");
            Check(Validator.TryValidateProperty(value, new ValidationContext(new TMCWD.Model.Administrator.UserWriteRequest())
                { MemberName = "Role" }, null), "Valid canonical role was rejected by user-management validation.");
        }
        foreach (var invalid in new[] { 0, 11, 999 })
            Check(!Validator.TryValidateProperty(invalid, new ValidationContext(new TMCWD.Model.Administrator.UserWriteRequest())
                { MemberName = "Role" }, null), "Unknown role was accepted by user-management validation.");
        var authorization = services.GetRequiredService<IAuthorizationService>();
        var policyRoles = new Dictionary<string, int[]>
        {
            ["Collection"] = [1, 7, 8], ["BillingOperations"] = [1, 2, 6, 7, 8],
            ["CustomerService"] = [1, 2, 6], ["ConfirmDisconnection"] = [1, 6],
            ["ConfirmReconnection"] = [1, 6], ["MeterReader"] = [4], ["SuperAdmin"] = [1],
            ["PendingAuthorizationDecision"] = []
        };
        foreach (var (policy, allowed) in policyRoles)
        {
            foreach (var role in roles)
            {
                var principal = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, ((int)role).ToString())], "test"));
                Check((await authorization.AuthorizeAsync(principal, null, policy)).Succeeded == allowed.Contains((int)role),
                    $"Production {policy} role membership changed for {role}.");
            }
            Check(!(await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), null, policy)).Succeeded,
                "Anonymous policy access allowed.");
        }
        foreach (var legacyName in new[] { "Cashier", "Billing" })
            Check(!(await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.Role, legacyName)], "test")), null, "Collection")).Succeeded,
                "String role aliases became canonical claims.");
        var key = new byte[32];
        var persistedSeven = UserSecurityStamp.Create(key, 1, "synthetic", 7, true, true, null, null);
        Check(persistedSeven == UserSecurityStamp.Create(key, 1, "synthetic", (int)UserRole.Billing, true, true, null, null)
            && persistedSeven != UserSecurityStamp.Create(key, 1, "synthetic", (int)UserRole.BillingHead, true, true, null, null),
            "Rename invalidated numeric Role=7 stamps or role promotion retained freshness.");
        Console.WriteLine("PASS: canonical role values, admin choices, numeric session/JSON/stamp compatibility and all production policy role/anonymous boundaries.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
