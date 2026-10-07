using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TMCWD.Administration;
using TMCWD.Application.Controllers;
using TMCWD.Model.Administrator;
using TMCWD.Services;
using TMCWD.Utility.Encryption;

const string password = "Synthetic test password";
const string key = "Synthetic-only-signing-key-for-regression-0123456789";
var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Jwt:Key"] = key, ["Jwt:Issuer"] = "synthetic-tmcwd",
    ["Jwt:Audience"] = "synthetic-reader"
}).Build();
using var handler = new SyntheticUsersHandler();
using var client = new HttpClient(handler) { BaseAddress = new Uri("https://synthetic.invalid/") };
var webService = new WebService(new SyntheticClientFactory(client));
var controller = new AuthController(new ApplicationLoginTransaction(webService),
    configuration, new UserTransaction(webService));

async Task<IActionResult> Login(string identifier, string secret = password) =>
    await controller.MeterReaderLogin(new() { Identifier = identifier, Password = secret });
void Check(bool result, string description)
{
    if (!result) throw new Exception(description);
}
void Status(IActionResult result, int expected, string description)
{
    var actual = result switch
    {
        ObjectResult value => value.StatusCode ?? 200,
        StatusCodeResult value => value.StatusCode,
        _ => -1
    };
    Check(actual == expected, description);
}

foreach (var identifier in new[] { "reader@example.invalid", "10", "  reader@example.invalid  " })
{
    var result = await Login(identifier);
    Status(result, 200, "Active reader must authenticate by email or TMCWD user ID.");
    var response = JsonSerializer.SerializeToElement(((OkObjectResult)result).Value);
    var token = response.GetProperty("token").GetString()!;
    var principal = new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = "synthetic-tmcwd",
        ValidateAudience = true, ValidAudience = "synthetic-reader",
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ValidateLifetime = true, ClockSkew = TimeSpan.Zero
    }, out var validated);
    Check(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value == "10", "JWT reader identity mismatch.");
    Check(principal.IsInRole(((int)UserRole.MeterReader).ToString()), "JWT meter-reader role mismatch.");
    Check(response.GetProperty("userId").GetInt32() == 10, "Response identity mismatch.");
    var expiry = response.GetProperty("expiresAt").GetDateTime();
    Check(expiry == validated.ValidTo && expiry.Kind == DateTimeKind.Utc, "Device expiry must match JWT expiry.");
    Check(expiry > DateTime.UtcNow.AddDays(29) && expiry <= DateTime.UtcNow.AddDays(30),
        "Reader token lifetime must preserve the existing thirty-day policy.");
    Check(!response.TryGetProperty("password", out _), "Login must never return a password.");
}

Status(await Login("reader@example.invalid", "wrong"), 401, "Wrong password accepted.");
Status(await Login("unknown@example.invalid"), 401, "Unknown email accepted.");
Status(await Login("999"), 401, "Unknown numeric ID accepted.");
Status(await Login("0"), 401, "Nonpositive ID accepted.");
Status(await Login(""), 400, "Empty identifier accepted.");
Status(await Login("   "), 400, "Whitespace identifier accepted.");
Status(await Login("reader@example.invalid", ""), 400, "Empty password accepted.");
Status(await Login(new string('x', 101)), 400, "Overlong identifier accepted.");
Status(await Login("reader@example.invalid", new string('x', 257)), 400, "Overlong password accepted.");
Status(await Login("inactive@example.invalid"), 403, "Inactive reader accepted.");
Status(await Login("unknown-active@example.invalid"), 403, "Null active flag accepted.");
foreach (var role in Enum.GetValues<UserRole>().Where(role => role != UserRole.MeterReader))
    Status(await Login($"role{(int)role}@example.invalid"), 403, "Non-reader role accepted.");
Status(await Login("11"), 401, "ID lookup resolving to a different identity accepted.");
Status(await controller.Login(new() { Email = "role3@example.invalid", Password = password }), 200,
    "Existing general login behavior changed.");
Console.WriteLine("PASS: reader credentials, input validation, active role, signed identity/expiry and legacy login.");

sealed class SyntheticClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}

sealed class SyntheticUsersHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath);
        var identifier = path.Split('/').Last();
        User? user = identifier switch
        {
            "reader@example.invalid" or "10" => Make(10, "reader@example.invalid", 4, true),
            // Mimic an inconsistent/nonunique email mapping; it must not grant a different identity.
            "11" => Make(11, "reader@example.invalid", 4, true),
            "inactive@example.invalid" => Make(20, identifier, 4, false),
            "unknown-active@example.invalid" => Make(21, identifier, 4, null),
            _ when identifier.StartsWith("role") && int.TryParse(identifier[4..5], out var role)
                => Make(30 + role, identifier, role, true),
            _ => null
        };
        return Task.FromResult(new HttpResponseMessage(user == null ? HttpStatusCode.NotFound : HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(user), Encoding.UTF8, "application/json")
        });
    }

    private static User Make(int id, string email, int role, bool? active) => new()
    {
        Id = id, Email = email, Name = "Synthetic reader", Role = role, IsActive = active,
        Password = StringEncyption.Encrypt("Synthetic test password")
    };
}

