using System.Text;
using Microsoft.IdentityModel.Tokens;
using TMCWD.Services;

namespace TMCWD.Application;

public static class ApiClientRegistration
{
    public static IServiceCollection AddSessionApiClient(this IServiceCollection services)
    {
        services.AddScoped<WebService>(services =>
        {
            var service = new WebService(services.GetRequiredService<IHttpClientFactory>());
            var user = services.GetRequiredService<AuthenticatedUserService>().User;
            var stamp = services.GetRequiredService<AuthenticatedUserService>().SecurityStamp;
            if (user.Id > 0 && user.Role.HasValue && !string.IsNullOrWhiteSpace(stamp))
            {
                // Bridge the trusted server session to the API; never share user headers across requests.
                var config = services.GetRequiredService<IConfiguration>();
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var token = handler.CreateToken(new SecurityTokenDescriptor {
                    Subject = new System.Security.Claims.ClaimsIdentity([
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, user.Role.Value.ToString()),
                        new System.Security.Claims.Claim(TMCWD.Utility.Encryption.UserSecurityStamp.ClaimType, stamp)]),
                    Issuer = config["Jwt:Issuer"], Audience = config["Jwt:Audience"], Expires = DateTime.UtcNow.AddMinutes(5),
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)), SecurityAlgorithms.HmacSha256)
                });
                service.Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", handler.WriteToken(token));
            }
            return service;
        });
        return services;
    }
}
