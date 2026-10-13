using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;
using TMCWD.Data.Context;
using TMCWD.Data.Controllers;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;
using TMCWD.Model.Billing;
using TMCWD.Utility.Encryption;

// Explicit opt-in: source connection is read-only; all posting writes use the existing test fixture.
static class DevelopmentDeploymentVerification
{
    public static async Task Run(UserDbContext fixture, ReadingSheetService sheets, ReadingService readings,
        Func<string, Func<Task>, Task> test)
    {
        if (fixture.Database.GetDbConnection().Database != "tmcwd_test") throw new Exception("Dedicated test database required.");
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft/UserSecrets/TMCWD.Data-local-development/secrets.json");
        using var secrets = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        string Setting(string name) => secrets.RootElement.GetProperty(name).GetString()!;
        var source = new MySqlConnectionStringBuilder(Setting("ConnectionStrings:DefaultConnection"));
        if (source.Server != "127.0.0.1" || source.Port != 3307 || source.Database != "tmcwd")
            throw new Exception("Unexpected development source.");
        await using var db = new UserDbContext(new DbContextOptionsBuilder<UserDbContext>()
            .UseMySql(source.ConnectionString, new MySqlServerVersion(new Version(8, 0, 46))).Options);
        await db.Database.OpenConnectionAsync();
        await using (var identity = db.Database.GetDbConnection().CreateCommand())
        {
            identity.CommandText = "SELECT CONCAT_WS('|',DATABASE(),@@port,@@server_uuid,VERSION())";
            Require((string)(await identity.ExecuteScalarAsync())! ==
                "tmcwd|3307|c3f950fe-b565-11f1-b6a6-30560f67a354|8.0.46", "Unexpected source identity.");
        }
        var service = new MeterMaintenanceRateService(db);
        var rates = await service.GetAll();
        Require(rates.Count == 1, "Exactly one deployed configuration required.");
        var rate = rates.Single();
        Require(rate.Amount == 20m && rate.EffectiveFrom == new DateOnly(2025, 1, 1)
            && rate.EffectiveTo == null && rate.CreatedBy == 5 && rate.UpdatedBy == 5, "Incorrect deployed rate.");
        foreach (var date in new[] { new DateOnly(2025, 1, 1), new DateOnly(2025, 7, 1),
            new DateOnly(2026, 1, 1), DateOnly.FromDateTime(DateTime.Today), new DateOnly(2030, 1, 1) })
            Require((await service.GetForBillingDate(date)).Id == rate.Id, "Deployed boundary lookup failed.");
        var missing = false;
        try { await service.GetForBillingDate(new DateOnly(2024, 12, 31)); }
        catch (KeyNotFoundException) { missing = true; }
        Require(missing, "Historical missing rate must fail closed.");
        var july = await db.ReadingSheets.AsNoTracking().Where(s => s.Id == 2 || s.Id == 5).ToListAsync();
        Require(july.Count == 2, "July sheets missing.");
        foreach (var sheet in july)
            Require(sheet.BillingDate == new DateTime(2025, 7, 1)
                && (await service.GetForBillingDate(DateOnly.FromDateTime(sheet.BillingDate.Value))).Amount == 20m,
                "July sheet deployed lookup failed.");
        Console.WriteLine("PASS: actual development service exact row, six boundaries and July sheets 2/5.");

        var key = Encoding.UTF8.GetBytes(Setting("Jwt:Key"));
        var issuer = Setting("Jwt:Issuer"); var audience = Setting("Jwt:Audience");
        var validation = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = issuer,
            ValidateAudience = true, ValidAudience = audience, ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key), ValidateLifetime = true,
            RequireExpirationTime = true, RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.Zero };
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
        builder.Services.AddSingleton(db);
        builder.Services.AddSingleton<IMeterMaintenanceRateService>(service);
        builder.Services.AddControllers().AddApplicationPart(typeof(MeterMaintenanceRateController).Assembly);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => {
            options.TokenValidationParameters = validation;
            options.Events = new JwtBearerEvents { OnTokenValidated = PersistedTokenValidation.Validate };
        });
        builder.Services.AddAuthorization(options => {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            options.AddPolicy("BillingOperations", policy => policy.RequireAuthenticatedUser().RequireRole("1","2","6","7"));
            options.AddPolicy("SuperAdmin", policy => policy.RequireAuthenticatedUser().RequireRole("1"));
        });
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            async Task<HttpResponseMessage> Get(string route, User? actor)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, route);
                if (actor != null)
                {
                    var handler = new JwtSecurityTokenHandler();
                    var token = handler.CreateToken(new SecurityTokenDescriptor {
                        Subject = new ClaimsIdentity([
                            new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString()),
                            new Claim(ClaimTypes.Role, actor.Role!.Value.ToString()),
                            new Claim(UserSecurityStamp.ClaimType, UserSecurityStamp.Create(key, actor.Id,
                                actor.Password, actor.Role, actor.IsActive, actor.IsVerified, actor.DateUpdated, actor.DateVerified))]),
                        Issuer = issuer, Audience = audience, Expires = DateTime.UtcNow.AddMinutes(2),
                        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key),SecurityAlgorithms.HmacSha256) });
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", handler.WriteToken(token));
                }
                return await client.SendAsync(request);
            }
            var admin = await db.Users.AsNoTracking().SingleAsync(u => u.Id == 5);
            var reader = await db.Users.AsNoTracking().FirstAsync(u => u.Role == 4 && u.IsActive == true && u.IsVerified == true);
            var staff = await db.Users.AsNoTracking().FirstAsync(u => u.Role == 6 && u.IsActive == true && u.IsVerified == true);
            const string lookup = "api/MeterMaintenanceRate/by-billing-date/2025-07-01";
            using var anonymous = await Get(lookup, null);
            using var wrongRole = await Get(lookup, reader);
            using var authorized = await Get(lookup, staff);
            using var configuration = await Get("api/MeterMaintenanceRate", admin);
            Require(anonymous.StatusCode == HttpStatusCode.Unauthorized && wrongRole.StatusCode == HttpStatusCode.Forbidden
                && authorized.StatusCode == HttpStatusCode.OK && configuration.StatusCode == HttpStatusCode.OK,
                "Actual development API authorization failed.");
            using var response = JsonDocument.Parse(await authorized.Content.ReadAsStringAsync());
            Require(response.RootElement.GetProperty("amount").GetDecimal() == 20m, "API returned wrong amount.");
            Console.WriteLine("PASS: real development controller/service HTTP GETs: anonymous 401, reader 403, billing role 200/20.00, SuperAdmin config 200.");
        }
        finally { await app.StopAsync(); }

        await test("Read-only deployed rate copy participates in isolated posting and retry", async () => {
            await fixture.MeterMaintenanceRates.ExecuteDeleteAsync();
            fixture.MeterMaintenanceRates.Add(new MeterMaintenanceRate { Id = rate.Id, Amount = rate.Amount,
                EffectiveFrom = rate.EffectiveFrom, EffectiveTo = rate.EffectiveTo, CreatedBy = rate.CreatedBy,
                UpdatedBy = rate.UpdatedBy, DateCreated = rate.DateCreated, DateUpdated = rate.DateUpdated });
            await fixture.SaveChangesAsync();
            await readings.SaveUpdate(1, new TMCWD.Data.Entities.Reading { AccountId = 1, ReadingSheetId = 1,
                CurrentReading = 0, PreviousReading = 0, Status = ReadingStatus.InProgress, DateCreated = DateTime.Now });
            Require(await sheets.PartialPost(1, 1) == 1, "Posting failed.");
            var bill = await fixture.Billings.SingleAsync(); var frozen = bill.InvoiceSnapshotJson;
            var number = bill.ServiceInvoiceNumber;
            var snapshot = JsonSerializer.Deserialize<OfficialInvoiceSnapshot>(frozen!)!;
            Require(bill.TotalBillAmount == 120m && bill.RemainingAmount == 120m
                && snapshot.BillAmount == 100m && snapshot.MeterMaintenance == 20m
                && LatePaymentPenalty.Calculate(snapshot.BillAmount) == 10m
                && !await fixture.OtherCharges.AnyAsync(), "Deployed-rate financial invariant failed.");
            Require(await sheets.PartialPost(1, 1) == 0 && await fixture.Billings.CountAsync() == 1,
                "Posting retry duplicated billing.");
            fixture.ChangeTracker.Clear(); bill = await fixture.Billings.SingleAsync();
            Require(bill.ServiceInvoiceNumber == number && bill.InvoiceSnapshotJson == frozen
                && bill.TotalBillAmount == 120m, "Retry changed invoice/snapshot.");
        });
    }
    private static void Require(bool result, string message) { if (!result) throw new Exception(message); }
}
