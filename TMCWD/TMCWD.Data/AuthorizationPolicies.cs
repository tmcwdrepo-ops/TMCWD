using System.Globalization;
using Microsoft.AspNetCore.Authorization;

namespace TMCWD.Data;

public static class AuthorizationPolicies
{
    public static void Configure(AuthorizationOptions options)
    {
        options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        // ponytail: Legacy operations stay denied until their business policies and trust boundaries are approved and implemented.
        options.AddPolicy("PendingAuthorizationDecision", policy => policy.RequireAuthenticatedUser()
            .RequireAssertion(_ => false));
        options.AddPolicy(nameof(UserRole.MeterReader), policy => policy.RequireAuthenticatedUser()
            .RequireRole(((int)UserRole.MeterReader).ToString(CultureInfo.InvariantCulture)));
        options.AddPolicy("Collection", policy => policy.RequireAuthenticatedUser().RequireRole(
            ((int)UserRole.Billing).ToString(CultureInfo.InvariantCulture),
            ((int)UserRole.BillingHead).ToString(CultureInfo.InvariantCulture),
            ((int)UserRole.SuperAdmin).ToString(CultureInfo.InvariantCulture)));
        options.AddPolicy(nameof(UserRole.SuperAdmin), policy => policy.RequireAuthenticatedUser()
            .RequireRole(((int)UserRole.SuperAdmin).ToString(CultureInfo.InvariantCulture)));
        options.AddPolicy("BillingOperations", policy => policy.RequireAuthenticatedUser().RequireRole(
            ((int)UserRole.SuperAdmin).ToString(CultureInfo.InvariantCulture),
            ((int)UserRole.CustomerRepresentative).ToString(CultureInfo.InvariantCulture),
            ((int)UserRole.CustomerServiceOic).ToString(CultureInfo.InvariantCulture),
            ((int)UserRole.Billing).ToString(CultureInfo.InvariantCulture),
            ((int)UserRole.BillingHead).ToString(CultureInfo.InvariantCulture)));
        options.AddPolicy("CustomerService", policy => policy.RequireAuthenticatedUser().RequireRole(
            ((int)UserRole.SuperAdmin).ToString(CultureInfo.InvariantCulture),
            ((int)UserRole.CustomerRepresentative).ToString(CultureInfo.InvariantCulture),
            ((int)UserRole.CustomerServiceOic).ToString(CultureInfo.InvariantCulture)));
        options.AddPolicy("ConfirmDisconnection", policy => policy.RequireAuthenticatedUser().RequireRole(
            ((int)UserRole.CustomerServiceOic).ToString(CultureInfo.InvariantCulture),
            ((int)UserRole.SuperAdmin).ToString(CultureInfo.InvariantCulture)));
        options.AddPolicy("ConfirmReconnection", policy => policy.RequireAuthenticatedUser().RequireRole(
            ((int)UserRole.CustomerServiceOic).ToString(CultureInfo.InvariantCulture),
            ((int)UserRole.SuperAdmin).ToString(CultureInfo.InvariantCulture)));
    }
}
