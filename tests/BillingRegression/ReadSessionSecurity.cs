using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using TMCWD.Application;
using TMCWD.Data.Context;
using TMCWD.Data.Controllers;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;
using TMCWD.Services;
using TMCWD.Utility.Encryption;

static class ReadSessionSecurity
{
    public static async Task Run(UserDbContext db, ReadingSheetService sheets, string key, string issuer,
        string audience, Func<string, Func<Task>, Task> test, Func<Task<string>> state)
    {
        if (db.Database.GetDbConnection().Database != "tmcwd_test") throw new Exception("B4 requires tmcwd_test.");
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(db);
        builder.Services.AddSingleton<IUserService>(new UserService(db));
        builder.Services.AddSingleton<IAccountService>(new AccountService(db));
        builder.Services.AddSingleton<ICustomerService>(new CustomerService(db));
        builder.Services.AddSingleton<IBillingService>(new BillingService(db));
        builder.Services.AddSingleton<AccountLedgerService>();
        builder.Services.AddSingleton<DisconnectionMonitoringService>();
        builder.Services.AddSingleton<IPaymentService>(new PaymentService(db));
        builder.Services.AddSingleton<IReadingService>(new ReadingService(db, new AccountService(db)));
        builder.Services.AddSingleton<IReadingSheetService>(sheets);
        builder.Services.AddSingleton<IZoneBookService>(new ZoneBookService(db));
        builder.Services.AddSingleton<IReadingSheetTemplateService>(new ReadingSheetTemplateService(db));
        builder.Services.AddSingleton<IInspectionTypeService>(new InspectionTypeService(db));
        builder.Services.AddControllers().AddApplicationPart(typeof(AccountController).Assembly);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => {
            options.IncludeErrorDetails = false;
            options.TokenValidationParameters = new TokenValidationParameters {
                ValidateIssuer = true, ValidIssuer = issuer, ValidateAudience = true, ValidAudience = audience,
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.Zero };
            options.Events = new JwtBearerEvents { OnTokenValidated = PersistedTokenValidation.Validate };
        });
        builder.Services.AddAuthorization(TMCWD.Data.AuthorizationPolicies.Configure);
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            var routes = ((IEndpointRouteBuilder)app).DataSources.SelectMany(s => s.Endpoints).OfType<RouteEndpoint>()
                .Where(e => e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("GET") == true
                    || e.RoutePattern.RawText == "api/Reading/GetRangeByReadingSheetIds")
                .OrderBy(e => e.RoutePattern.RawText).ToArray();
            var inventory = new List<string> { "| Method / route | Classification | Effective policy |", "|---|---|---|" };
            foreach (var endpoint in routes)
            {
                var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(x => x.Policy)
                    .Where(x => !string.IsNullOrEmpty(x)).Distinct().ToArray();
                var effective = policies.Contains("PendingAuthorizationDecision") ? "PendingAuthorizationDecision"
                    : policies.Contains("Collection") ? "Collection" : policies.Contains("SuperAdmin") ? "SuperAdmin"
                    : policies.Contains("CustomerService") ? "CustomerService" : policies.FirstOrDefault() ?? "Authenticated";
                var classification = effective switch { "PendingAuthorizationDecision" => "BLOCKED — POLICY REQUIRED",
                    "BillingOperations" => "BILLING OPERATIONS", "CustomerService" => "CUSTOMER SERVICE", "Collection" => "COLLECTION",
                    "SuperAdmin" => "SUPERADMIN", "MeterReader" => "METER READER", _ => "AUTHENTICATED" };
                inventory.Add($"| `{string.Join('/', endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods)} /{endpoint.RoutePattern.RawText}` | {classification} | {effective} |");
            }
            await File.WriteAllLinesAsync(".tmp/phase6ib4-read-inventory.md", inventory);
            async Task Actors()
            {
                foreach (var (id, role) in new[] { (5, 3), (7, 7), (8, 8), (9, 9), (12, 10), (10, 4), (11, 4) })
                    if (!await db.Users.AnyAsync(u => u.Id == id)) db.Users.Add(new User { Id = id,
                        Name = "Synthetic B4 actor", Email = $"b4-{id}@test.invalid", Role = role,
                        Password = StringEncyption.Encrypt("synthetic-current"), IsActive = true, IsVerified = true });
                await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            }
            string Stamp(User user) => UserSecurityStamp.Create(Encoding.UTF8.GetBytes(key), user.Id, user.Password,
                user.Role, user.IsActive, user.IsVerified, user.DateUpdated, user.DateVerified);
            async Task<string> Token(int id, string? stamp = null, int? role = null, bool omitStamp = false)
            {
                var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
                var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, id.ToString(CultureInfo.InvariantCulture)),
                    new(ClaimTypes.Role, (role ?? user.Role)!.Value.ToString(CultureInfo.InvariantCulture)) };
                if (!omitStamp) claims.Add(new(UserSecurityStamp.ClaimType, stamp ?? Stamp(user)));
                var handler = new JwtSecurityTokenHandler();
                return handler.WriteToken(handler.CreateToken(new SecurityTokenDescriptor { Subject = new ClaimsIdentity(claims),
                    Issuer = issuer, Audience = audience, Expires = DateTime.UtcNow.AddMinutes(5),
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256) }));
            }
            async Task<HttpResponseMessage> Send(string path, string? token, HttpMethod? method = null, object? body = null)
            {
                using var request = new HttpRequestMessage(method ?? HttpMethod.Get, path);
                if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                if (body != null) request.Content = JsonContent.Create(body);
                return await client.SendAsync(request);
            }
            async Task Reject(string path, string? token, HttpStatusCode expected, HttpMethod? method = null, object? body = null)
            {
                var before = await state();
                var usersBefore = JsonSerializer.Serialize(await db.Users.AsNoTracking().Select(u => new { u.Id, u.Password, u.RememberToken }).ToListAsync());
                using var response = await Send(path, token, method, body);
                Check(response.StatusCode == expected, $"B4 rejection {path}: {response.StatusCode}, expected {expected}.");
                db.ChangeTracker.Clear();
                Check(await state() == before && usersBefore == JsonSerializer.Serialize(await db.Users.AsNoTracking()
                    .Select(u => new { u.Id, u.Password, u.RememberToken }).ToListAsync()), "Rejected B4 request changed financial/user state.");
            }
            foreach (var endpoint in routes)
            {
                var pattern = endpoint.RoutePattern.RawText!;
                var path = Regex.Replace(pattern, "\\{([^}]+)\\}", m => {
                    var name = m.Groups[1].Value.Split(':')[0];
                    return name.Contains("Date", StringComparison.OrdinalIgnoreCase) || name.Contains("Period", StringComparison.OrdinalIgnoreCase)
                        ? "2025-01-01" : name == "meterSize" ? "0.5" : "1";
                });
                var method = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains("GET") ? HttpMethod.Get : HttpMethod.Post;
                await test($"HTTP B4 anonymous sensitive query rejected: {pattern}", () => Reject(path, null,
                    HttpStatusCode.Unauthorized, method, method == HttpMethod.Post ? new[] { 1 } : null));
                await test($"HTTP B4 inappropriate role rejected: {pattern}", async () => {
                    await Actors();
                    await Reject(path, await Token(5), HttpStatusCode.Forbidden, method, method == HttpMethod.Post ? new[] { 1 } : null);
                });
            }
            foreach (var (id, role) in new[] { (7, UserRole.Billing), (8, UserRole.BillingHead),
                (9, UserRole.GeneralServices), (12, UserRole.Accounting) })
                await test("HTTP numeric JWT persisted role boundary: " + role, async () => {
                    await Actors();
                    using var response = await Send("api/Billing/Collection/TEST", await Token(id));
                    Check(role is UserRole.Billing or UserRole.BillingHead
                        ? response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound
                        : response.StatusCode == HttpStatusCode.Forbidden, "Numeric new-role JWT boundary failed.");
                    await Reject("api/DebitCreditMemo/GetAll", await Token(id), HttpStatusCode.Forbidden);
                    await Reject("api/Users/SaveUpdate/999", await Token(id), HttpStatusCode.Forbidden, HttpMethod.Post,
                        new { id, role = (int)UserRole.SuperAdmin });
                    var stale = await Token(id);
                    var user = await db.Users.SingleAsync(u => u.Id == id);
                    user.Role = (int)UserRole.Guest;
                    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
                    await Reject("api/Billing/Collection/TEST", stale, HttpStatusCode.Unauthorized);
                });
            foreach (var id in new[] { 1, 2, 3, 7, 8 })
                await test($"HTTP B4 established staff {id} customer/account/billing/reading/directory access", async () => {
                    await Actors(); var token = await Token(id);
                    foreach (var path in new[] { "api/Account/Get/1", "api/Customer/Get/1", "api/Billing/GetAll",
                        "api/ReadingSheet/Get/1", "api/Users/GetUsers", "api/Users/Get/1", "api/Users/GetUsersById?ids=1", "api/DisconnectionMonitoring/TEST" })
                    {
                        using var response = await Send(path, token);
                        Check(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound, $"Staff read failed: {path} / {response.StatusCode}.");
                        var body = await response.Content.ReadAsStringAsync();
                        Check(!body.Contains("password", StringComparison.OrdinalIgnoreCase) && !body.Contains("rememberToken", StringComparison.OrdinalIgnoreCase), "Read returned secret material.");
                        if (id != 3 && path.Contains("Users"))
                            Check(!body.Contains("email", StringComparison.OrdinalIgnoreCase) && !body.Contains("isVerified", StringComparison.OrdinalIgnoreCase)
                                && !body.Contains("role", StringComparison.OrdinalIgnoreCase), "Directory returned unnecessary profile/security fields.");
                    }
                });
            await test("HTTP B4 admin profile is password-free; nonadmin email/security lookup denied", async () => {
                using var result = await Send("api/Users/Get/1", await Token(3));
                Check(result.StatusCode == HttpStatusCode.OK, "Admin lookup failed.");
                var body = await result.Content.ReadAsStringAsync();
                Check(body.Contains("isVerified") && !body.Contains("password", StringComparison.OrdinalIgnoreCase)
                    && !body.Contains("rememberToken", StringComparison.OrdinalIgnoreCase), "Admin profile contract/secret boundary failed.");
                await Reject("api/Users/GetByEmail/csr@test.invalid", await Token(1), HttpStatusCode.Forbidden);
                await Reject("api/Users/GetUsersByRole/1", await Token(1), HttpStatusCode.Forbidden);
            });
            await test("HTTP B4 current authorization posts and reads the immutable financial invoice and Collections history", async () => {
                await Actors();
                (await db.Accounts.SingleAsync()).Status = (int)AccountStatus.Active;
                (await db.ReadingSheets.SingleAsync()).AssignedTo = 10;
                db.Readings.Add(new Reading { AccountId = 1, ReadingSheetId = 1, CurrentReading = 10,
                    PreviousReading = 0, Status = ReadingStatus.InProgress, DateCreated = DateTime.Now });
                await db.SaveChangesAsync(); db.ChangeTracker.Clear();
                var token = await Token(1);
                using var posted = await Send("api/ReadingSheet/PartialPostReadingSheet?readingSheetId=1&userId=999", token, HttpMethod.Post);
                Check(posted.IsSuccessStatusCode, "Current security stamp blocked legitimate posting.");
                var invoice = await db.Billings.AsNoTracking().SingleAsync();
                Check(invoice.CreatedBy == 1 && invoice.TotalBillAmount == 120m && invoice.RemainingAmount == 120m,
                    "Current posting changed financial calculation or audit identity.");
                var before = await state();
                foreach (var path in new[] { "api/Billing/GetByAccountId/1", $"api/Billing/Get/{invoice.Id}",
                    $"api/Billing/Collection/{invoice.BillingReferenceId}", "api/Billing/AccountLedger/TEST",
                    "api/Billing/History/TEST", "api/Payment/GetByDateRange?from=2025-01-01&to=2027-01-01" }) {
                    using var read = await Send(path, await Token(7));
                    Check(read.IsSuccessStatusCode, $"Authorized financial read failed: {path} / {read.StatusCode}.");
                }
                using (var soa = await Send("api/reading-sheet/mobile/1/accounts/1/soa", await Token(10))) {
                    Check(soa.IsSuccessStatusCode, "Assigned reader official SOA access failed.");
                    var issued = JsonDocument.Parse(await soa.Content.ReadAsStringAsync());
                    Check(issued.RootElement.GetProperty("meterMaintenance").GetDecimal() == 20m,
                        "Official SOA changed the issued maintenance amount.");
                }
                Check(await state() == before && (await db.Billings.AsNoTracking().SingleAsync()).InvoiceSnapshotJson == invoice.InvoiceSnapshotJson,
                    "Financial GET changed issued state.");
            });
            foreach (var change in new[] { "role", "inactive", "unverified", "password", "profile-update", "edit-revert" })
                await test($"HTTP B4 stale JWT and original MVC session rejected after {change}", async () => {
                    var original = await db.Users.AsNoTracking().SingleAsync(u => u.Id == 3);
                    var token = await Token(3);
                    using (var allowed = await Send("api/Account/Get/1", token)) Check(allowed.IsSuccessStatusCode, "Unchanged actor failed.");
                    var user = await db.Users.SingleAsync(u => u.Id == 3);
                    if (change == "role") user.Role = (int)UserRole.CustomerRepresentative;
                    if (change == "inactive") user.IsActive = false;
                    if (change == "unverified") user.IsVerified = false;
                    if (change == "password") user.Password = StringEncyption.Encrypt("synthetic-new");
                    if (change is "profile-update" or "edit-revert") {
                        user.Name = "Changed synthetic profile";
                        await new UserService(db).SaveUpdate(3, user);
                        if (change == "edit-revert") { user.Name = original.Name; await new UserService(db).SaveUpdate(3, user); }
                    }
                    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
                    await Reject("api/Account/Get/1", token, HttpStatusCode.Unauthorized);
                    await Reject("api/ReadingSheet/PartialPostReadingSheet?readingSheetId=1&userId=999", token,
                        HttpStatusCode.Unauthorized, HttpMethod.Post);
                    var services = new ServiceCollection(); services.AddLogging();
                    services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                        ["Jwt:Key"] = key, ["Jwt:Issuer"] = issuer, ["Jwt:Audience"] = audience }).Build());
                    var session = new AuthenticatedUserService { SecurityStamp = Stamp(original) };
                    session.SetUser(new TMCWD.Model.Administrator.User { Id = 3, Role = original.Role });
                    services.AddSingleton(session); services.AddHttpClient("TmcWdApi", c => c.BaseAddress = client.BaseAddress);
                    services.AddSessionApiClient(); await using var provider = services.BuildServiceProvider();
                    await using var scope = provider.CreateAsyncScope();
                    using var stale = await scope.ServiceProvider.GetRequiredService<WebService>().Client.GetAsync("api/Account/Get/1");
                    Check(stale.StatusCode == HttpStatusCode.Unauthorized, "Fresh MVC bridge token resurrected stale session authorization.");
                });
            await test("HTTP B4 current-password self change revokes previously issued authorization", async () => {
                var user = await db.Users.SingleAsync(u => u.Id == 1);
                user.Password = StringEncyption.Encrypt("synthetic-current"); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
                var token = await Token(1);
                using var change = await Send("api/Users/password", token, HttpMethod.Post,
                    new { currentPassword = "synthetic-current", newPassword = "synthetic-new", confirmPassword = "synthetic-new" });
                Check(change.StatusCode == HttpStatusCode.NoContent, "Password command failed.");
                await Reject("api/Account/Get/1", token, HttpStatusCode.Unauthorized);
                using var renewed = await Send("api/auth/login", null, HttpMethod.Post,
                    new { identifier = "1", password = "synthetic-new" });
                Check(renewed.IsSuccessStatusCode, "New credentials failed.");
                await Reject("api/auth/login", null, HttpStatusCode.Unauthorized, HttpMethod.Post,
                    new { identifier = "1", password = "synthetic-current" });
            });
            foreach (var actorId in new[] { 2, 3 })
            await test("HTTP Phase 6J stale " + (actorId == 2 ? "CS OIC" : "SuperAdmin") + " fingerprint cannot confirm physical disconnection", async () => {
                var token = await Token(actorId);
                var actor = await db.Users.SingleAsync(u => u.Id == actorId); actor.IsActive = false;
                await db.SaveChangesAsync(); db.ChangeTracker.Clear();
                await Reject("api/DisconnectionMonitoring/TEST/disconnect", token, HttpStatusCode.Unauthorized,
                    HttpMethod.Post, new { operationId = Guid.NewGuid() });
                await Reject("api/DisconnectionMonitoring/TEST/reconnect", token, HttpStatusCode.Unauthorized,
                    HttpMethod.Post, new { operationId = Guid.NewGuid() });
                Check(!await db.ServiceDisconnectionEvents.AnyAsync() && !await db.OtherCharges.AnyAsync(), "Revoked CS OIC wrote event or fee.");
            });
            await test("HTTP B4 missing/tampered fingerprint and forged elevated role rejected", async () => {
                await Reject("api/Account/Get/1", await Token(1, omitStamp: true), HttpStatusCode.Unauthorized);
                await Reject("api/Account/Get/1", await Token(1, "forged"), HttpStatusCode.Unauthorized);
                await Reject("api/Users/Get/1", await Token(1, role: 1), HttpStatusCode.Unauthorized);
            });
            await test("HTTP B4 real credential login preserves unchanged MVC and Application reader bridges", async () => {
                await Actors();
                var actor = await db.Users.SingleAsync(u => u.Id == 1);
                actor.Password = StringEncyption.Encrypt("synthetic-current"); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
                var services = new ServiceCollection(); services.AddLogging();
                services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                    ["Jwt:Key"] = key, ["Jwt:Issuer"] = issuer, ["Jwt:Audience"] = audience }).Build());
                var session = new AuthenticatedUserService(); services.AddSingleton(session);
                services.AddHttpClient("TmcWdApi", c => c.BaseAddress = client.BaseAddress); services.AddSessionApiClient();
                await using var provider = services.BuildServiceProvider();
                await using (var scope = provider.CreateAsyncScope()) {
                    var login = new TMCWD.Administration.ApplicationLoginTransaction(scope.ServiceProvider.GetRequiredService<WebService>()) {
                        Email = "1", Password = "synthetic-current" };
                    var profile = await login.Login();
                    Check(profile.Id == 1 && !string.IsNullOrWhiteSpace(login.SecurityStamp) && string.IsNullOrEmpty(profile.Password), "Credential bridge failed.");
                    session.SetUser(profile); session.SecurityStamp = login.SecurityStamp;
                }
                await using (var scope = provider.CreateAsyncScope()) {
                    using var allowed = await scope.ServiceProvider.GetRequiredService<WebService>().Client.GetAsync("api/Account/Get/1");
                    Check(allowed.IsSuccessStatusCode, "Unchanged MVC session bridge failed.");
                    var web = scope.ServiceProvider.GetRequiredService<WebService>();
                    var login = new TMCWD.Administration.ApplicationLoginTransaction(web);
                    var controller = new TMCWD.Application.Controllers.AuthController(login, scope.ServiceProvider.GetRequiredService<IConfiguration>(),
                        new TMCWD.Administration.UserTransaction(web));
                    var result = await controller.MeterReaderLogin(new() { Identifier = "10", Password = "synthetic-current" });
                    Check(result is Microsoft.AspNetCore.Mvc.OkObjectResult, "Application reader credential flow failed.");
                    var payload = JsonSerializer.SerializeToElement(((Microsoft.AspNetCore.Mvc.OkObjectResult)result).Value);
                    using var assigned = await Send("api/reading-sheet/mobile/assigned", payload.GetProperty("token").GetString());
                    Check(assigned.IsSuccessStatusCode, "Application-issued thirty-day token failed Data persisted validation.");
                }
            });
            foreach (var eligibility in new[] { "inactive", "unverified", "unknown-role" })
                await test($"HTTP B4 ineligible login cannot issue stamp/token: {eligibility}", async () => {
                    await Actors(); var user = await db.Users.SingleAsync(u => u.Id == 10);
                    if (eligibility == "inactive") user.IsActive = false;
                    if (eligibility == "unverified") user.IsVerified = false;
                    if (eligibility == "unknown-role") user.Role = 999;
                    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
                    foreach (var path in new[] { "api/auth/login", "api/auth/meter-reader/login" })
                        await Reject(path, null, HttpStatusCode.Forbidden, HttpMethod.Post,
                            new { identifier = "b4-10@test.invalid", password = "synthetic-current" });
                });
            await test("HTTP B4 Data-issued reader token preserves assignment boundary and revokes on deactivation", async () => {
                await Actors(); (await db.ReadingSheets.SingleAsync()).AssignedTo = 10; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
                using var login = await Send("api/auth/meter-reader/login", null, HttpMethod.Post,
                    new { identifier = "b4-10@test.invalid", password = "synthetic-current" });
                Check(login.IsSuccessStatusCode, "Reader login failed.");
                using var payload = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
                var token = payload.RootElement.GetProperty("token").GetString()!;
                using var assigned = await Send("api/reading-sheet/mobile/assigned", token);
                Check(assigned.IsSuccessStatusCode, "Reader assignment flow failed.");
                using var download = await Send("api/reading-sheet/mobile/1/download", token);
                Check(download.IsSuccessStatusCode, "Assigned reader download failed.");
                await Reject("api/reading-sheet/mobile/1/download", await Token(11), HttpStatusCode.Forbidden);
                await Reject("api/Account/Get/1", token, HttpStatusCode.Forbidden);
                (await db.Users.SingleAsync(u => u.Id == 10)).IsActive = false; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
                await Reject("api/reading-sheet/mobile/assigned", token, HttpStatusCode.Unauthorized);
            });
            Console.WriteLine($"B4 inventory: {routes.Length} query endpoints; anonymous and inappropriate-role HTTP checks for every row.");
        }
        finally { await app.StopAsync(); }
    }

    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
