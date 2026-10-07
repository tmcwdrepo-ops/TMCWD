using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TMCWD.Data.Context;
using TMCWD.Data.Services;
using System.Text.Json;

var config = new ConfigurationBuilder().AddJsonFile(Path.GetFullPath("TMCWD/TMCWD.Data/appsettings.json"), optional:true)
    .AddJsonFile(Path.GetFullPath("TMCWD/TMCWD.Data/appsettings.Development.json"), optional:true)
    .AddJsonFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft/UserSecrets/TMCWD.Data-local-development/secrets.json"), optional:true)
    .AddEnvironmentVariables().Build();
var connection = config.GetConnectionString("DefaultConnection") ?? throw new Exception("Local read-only application connection unavailable.");
var settings = new MySqlConnector.MySqlConnectionStringBuilder(connection);
if (!new[]{"localhost","127.0.0.1","::1"}.Contains(settings.Server)) throw new Exception("Only local read-only investigation allowed.");
await using var db = new UserDbContext(new DbContextOptionsBuilder<UserDbContext>().UseMySql(connection, new MySqlServerVersion(new Version(8,0,0))).Options);
var sheet = await db.ReadingSheets.AsNoTracking().SingleAsync(x=>x.Id==14);
var account = await db.Accounts.AsNoTracking().SingleAsync(x=>x.AccountNumber=="01-010015");
var roster = await new AccountService(db).GetReadingSheetRoster(sheet);
var readings = await db.Readings.AsNoTracking().Where(x=>x.ReadingSheetId==14 && x.AccountId==account.Id).ToListAsync();
var ids = readings.Select(x=>x.Id).ToList();
var posted = await db.Billings.AsNoTracking().Where(x=>ids.Contains(x.ReadingId)).Select(x=>new{x.Id,x.ReadingId}).ToListAsync();
Console.WriteLine(JsonSerializer.Serialize(new { sheetId=sheet.Id,sheet.BillingDate,sheet.BillingPeriodStart,sheet.AssignedTo,sheet.Status,accountId=account.Id, inRoster=roster.Any(x=>x.Id==account.Id), account.InitialReading, readings=readings.Select(x=>new{x.Id,x.PreviousReading,x.CurrentReading,x.Status,x.IsCompleted,x.Remarks,x.DateCreated}),posted}));
