using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MySqlConnector;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;
using TMCWD.Data.Controllers;


// These pure calculation checks run even when the database is unavailable.
await RoleModelRegression.Run();
foreach (var (bill, expected) in new[] { (1000m, 100m), (245.55m, 24.56m), (0m, 0m), (-10m, 0m), (100.04m, 10m) })
{
    if (TMCWD.Model.Billing.LatePaymentPenalty.Calculate(bill) != expected)
        throw new Exception($"Incorrect 10% penalty calculation for {bill}.");
}
Console.WriteLine("PASS: 5 penalty calculation and currency-rounding cases.");
var calculatorRate = new TMCWD.Model.Billing.WaterRate { MinimumCharge = 100m };
Check(TMCWD.Billing.WaterChargeCalculator.ComputeFromRate(0, 0, calculatorRate) == 100m
    && TMCWD.Billing.WaterChargeCalculator.ComputeBillAmount(0, 0, calculatorRate, 20m) == 120m,
    "The shared water calculator must keep maintenance explicit and apply it exactly once.");
var rateControllerType = typeof(MeterMaintenanceRateController);
Check(rateControllerType.GetMethod(nameof(MeterMaintenanceRateController.SaveUpdate))!
        .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)
        .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Single().Policy == nameof(UserRole.SuperAdmin)
    && rateControllerType.GetMethod(nameof(MeterMaintenanceRateController.GetForBillingDate))!
        .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)
        .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Single().Policy == "BillingOperations",
    "Maintenance configuration write/read authorization policies changed.");
Console.WriteLine("PASS: water-only calculator and explicit maintenance component.");

// Verify both optional-date query shapes translate on the real MySQL
// provider without opening a database connection.
await using (var queryDb = new UserDbContext(new DbContextOptionsBuilder<UserDbContext>()
    .UseMySql("Server=localhost;Database=billing_query_test;", new MySqlServerVersion(new Version(8, 0, 0))).Options))
{
    var queryService = new PenaltyService(queryDb);
    var factory = typeof(PenaltyService).GetMethod("ChargeEntries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
    foreach (var period in new DateTime?[] { null, DateTime.Today })
    {
        var query = (IQueryable<TMCWD.Model.Billing.PenaltyChargeEntry>)factory.Invoke(queryService,
            new object?[] { period, TMCWD.Model.Billing.LatePaymentPenalty.Today, null })!;
        _ = query.ToQueryString();
    }
}
Console.WriteLine("PASS: both penalty eligibility queries translate to MySQL.");

// All writes are confined to connection-local TEMPORARY tables. Never run
// tests against persistent customer records, even in a development database.
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
if (args.Length > 0) root = Path.GetFullPath(args[0]);
var runMigrationChecks = !args.Contains("--no-migrations", StringComparer.Ordinal);
var development6JBefore = args.Contains("--verify-disconnection-workflow", StringComparer.Ordinal) && args.Contains("--verify-development-deployment", StringComparer.Ordinal)
    ? await DisconnectionWorkflowRegression.InspectDevelopment() : null;
string? connectionString = Environment.GetEnvironmentVariable("BILLING_TEST_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("SKIPPED: configure BILLING_TEST_CONNECTION to run the temporary-table database checks.");
    Environment.ExitCode = 2;
    return;
}
var connectionOptions = new MySqlConnectionStringBuilder(connectionString);
if (!new[] { "localhost", "127.0.0.1", "::1" }.Contains(connectionOptions.Server))
    throw new Exception("Regression checks require a local MySQL server.");
if (!string.Equals(connectionOptions.Database, "tmcwd_test", StringComparison.Ordinal))
    throw new Exception("Database regressions are restricted to the dedicated tmcwd_test database.");
connectionOptions.AllowUserVariables = true;
connectionOptions.UseAffectedRows = false;
await using var connection = new MySqlConnection(connectionOptions.ConnectionString);
await connection.OpenAsync();
var failure = new FailAfterSave();
var options = new DbContextOptionsBuilder<UserDbContext>()
    .UseMySql(connection, ServerVersion.AutoDetect(connection)).AddInterceptors(failure).Options;
await using var db = new UserDbContext(options);
var tables = new[] { typeof(ReadingSheetTemplate), typeof(InspectionTypeDetail), typeof(InspectionReport), typeof(Recommendation), typeof(Material), typeof(Workflow), typeof(Finding), typeof(ApprovalHistory), typeof(RequestFile), typeof(AdvancePayment), typeof(PaymentCheck), typeof(ChargeType), typeof(Account), typeof(ReadingSheet), typeof(Reading), typeof(Billing), typeof(WaterRate), typeof(MeterMaintenanceRate), typeof(Penalty), typeof(OtherCharge), typeof(OtherChargeInstallment), typeof(OtherFeeType), typeof(Customer), typeof(Payment), typeof(PaymentReceipt), typeof(TMCWD.Data.Entities.BillingAdjustment), typeof(User), typeof(PenaltyWaiverRequest), typeof(Request), typeof(RequestDetail), typeof(JobOrder), typeof(InspectionType), typeof(ZoneBook), typeof(Inventory), typeof(BirInvoiceConfig), typeof(ServiceInvoiceSeq) }
    .Select(t => db.Model.FindEntityType(t)!.GetTableName()!)
    .Append(db.Model.FindEntityType(typeof(BillingAdjustmentOperation))!.GetTableName()!)
    .Append(db.Model.FindEntityType(typeof(ServiceDisconnectionEvent))!.GetTableName()!)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
foreach (var table in tables)
{
    // Build synthetic tables from the canonical EF model; the dedicated test
    // database need not contain a copy of the application's schema or data.
    {
        var ddl = System.Text.RegularExpressions.Regex.Match(db.Database.GenerateCreateScript(),
            $@"CREATE TABLE `{table}`[\s\S]*?;").Value;
        Check(ddl.Length > 0, "Missing synthetic BIR schema.");
        await using var create = new MySqlCommand(ddl.Replace("CREATE TABLE", "CREATE TEMPORARY TABLE"), connection);
        await create.ExecuteNonQueryAsync();
    }
}
// Allow the installation regression to run before the persistent schema migration.
if (runMigrationChecks)
{
await using (var inspectInvoice = new MySqlCommand("SHOW COLUMNS FROM Billings LIKE 'ServiceInvoiceNumber'", connection))
{
    if (await inspectInvoice.ExecuteScalarAsync() == null)
        await new MySqlCommand("ALTER TABLE Billings ADD COLUMN ServiceInvoiceNumber VARCHAR(30) NULL", connection).ExecuteNonQueryAsync();
    await using var index = new MySqlCommand("SHOW INDEX FROM Billings WHERE Key_name = 'UQ_Billings_ServiceInvoiceNumber'", connection);
    if (await index.ExecuteScalarAsync() == null)
        await new MySqlCommand("CREATE UNIQUE INDEX UQ_Billings_ServiceInvoiceNumber ON Billings (ServiceInvoiceNumber)", connection).ExecuteNonQueryAsync();
}
await using (var inspectRemarks = new MySqlCommand("SHOW COLUMNS FROM readings LIKE 'Remarks'", connection))
{
    if (await inspectRemarks.ExecuteScalarAsync() == null)
    {
        await using var alterRemarks = new MySqlCommand("ALTER TABLE readings ADD COLUMN Remarks VARCHAR(2000) NULL", connection);
        await alterRemarks.ExecuteNonQueryAsync(); // Connection-local temporary table only.
    }
}
// These ALTERs affect only the temporary request/detail/job tables above.
var serviceMigrationSql = await File.ReadAllTextAsync(Path.Combine(root,
    "TMCWD/TMCWD.Data/Database/20261002_migrate_service_requests.sql"));
var serviceSchema = System.Text.RegularExpressions.Regex.Replace(serviceMigrationSql.Split("-- DATA", 2)[0], @"(?m)^--.*$", "");
foreach (var statement in serviceSchema.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
{
    var column = System.Text.RegularExpressions.Regex.Match(statement, @"ALTER TABLE (\w+) ADD COLUMN (\w+)");
    if (column.Success)
    {
        await using var inspect = new MySqlCommand($"SHOW COLUMNS FROM `{column.Groups[1].Value}` LIKE '{column.Groups[2].Value}'", connection);
        if (await inspect.ExecuteScalarAsync() != null) continue;
    }
    var index = System.Text.RegularExpressions.Regex.Match(statement, @"ALTER TABLE (\w+) ADD UNIQUE INDEX (\w+)");
    if (index.Success)
    {
        await using var inspect = new MySqlCommand($"SHOW INDEX FROM `{index.Groups[1].Value}` WHERE Key_name = '{index.Groups[2].Value}'", connection);
        if (await inspect.ExecuteScalarAsync() != null) continue;
    }
    await using var alter = new MySqlCommand(statement, connection);
    await alter.ExecuteNonQueryAsync();
}
}
await using (var createApplicationsCommand = connection.CreateCommand())
{
    // The production table has foreign keys that the restricted regression user
    // cannot reproduce. This connection-local shape contains every mapped column
    // needed by the posting workflow without touching persistent applications.
    createApplicationsCommand.CommandText = @"
CREATE TEMPORARY TABLE `water_connection_applications` (
    `id` BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    `tracking_number` VARCHAR(100) NOT NULL,
    `full_name` VARCHAR(255) NOT NULL,
    `address` VARCHAR(500) NOT NULL,
    `barangay` VARCHAR(255) NOT NULL,
    `unit_number` VARCHAR(255) NULL,
    `building` VARCHAR(255) NULL,
    `house_number` VARCHAR(255) NULL,
    `street` VARCHAR(255) NULL,
    `contact_number` VARCHAR(100) NOT NULL,
    `email` VARCHAR(255) NOT NULL,
    `connection_type` VARCHAR(100) NOT NULL,
    `source` VARCHAR(100) NOT NULL,
    `stage` VARCHAR(50) NOT NULL,
    `account_number` VARCHAR(100) NULL,
    `customer_id` BIGINT NULL,
    `account_id` BIGINT NULL,
    `inspection_data` LONGTEXT NULL,
    `charging_data` LONGTEXT NULL,
    `payment_data` LONGTEXT NULL,
    `installation_data` LONGTEXT NULL,
    `connect_data` LONGTEXT NULL,
    `tapping_data` LONGTEXT NULL,
    `done_data` LONGTEXT NULL,
    `remarks` LONGTEXT NULL,
    `assigned_to` BIGINT NULL,
    `created_at` DATETIME(6) NULL,
    `updated_at` DATETIME(6) NULL
)";
    await createApplicationsCommand.ExecuteNonQueryAsync();
}
await using (var createDiscountCommand = connection.CreateCommand())
{
    createDiscountCommand.CommandText = @"
CREATE TEMPORARY TABLE `senior_citizen_discounts` (
    `Id` BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    `BillingReferenceId` VARCHAR(200) NOT NULL,
    `SeniorCitizenIdNumber` VARCHAR(30) NOT NULL,
    `SeniorCitizenName` VARCHAR(160) NOT NULL,
    `AccountHolderName` VARCHAR(160) NOT NULL,
    `Rate` DECIMAL(5,4) NOT NULL,
    `WaterCharge` DECIMAL(12,2) NOT NULL,
    `DiscountAmount` DECIMAL(12,2) NOT NULL,
    `CreatedBy` BIGINT NOT NULL,
    `DateCreated` DATETIME(6) NOT NULL,
    `IsCancelled` TINYINT(1) NOT NULL DEFAULT 0,
    `CancelledBy` BIGINT NOT NULL DEFAULT 0,
    `DateCancelled` DATETIME(6) NULL,
    KEY `IX_SeniorCitizenDiscount_BillingReference` (`BillingReferenceId`)
)";
    await createDiscountCommand.ExecuteNonQueryAsync();
}
await using (var createCancellationCommand = connection.CreateCommand())
{
    createCancellationCommand.CommandText = @"
CREATE TEMPORARY TABLE `invoice_cancellations` (
    `Id` BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    `PaymentReference` VARCHAR(200) NOT NULL,
    `Reason` VARCHAR(200) NOT NULL,
    `CancelledBy` BIGINT NOT NULL,
    `DateCancelled` DATETIME(6) NOT NULL,
    UNIQUE KEY `UX_InvoiceCancellation_PaymentReference` (`PaymentReference`)
)";
    await createCancellationCommand.ExecuteNonQueryAsync();
}
tables = tables.Append("water_connection_applications").Append("senior_citizen_discounts").Append("invoice_cancellations").ToArray();
var readingService = new ReadingService(db, new AccountService(db));
var maintenanceRateService = new MeterMaintenanceRateService(db);
var sheetService = new ReadingSheetService(db, new BillingService(db), new WaterRateService(db), new AccountService(db), readingService, maintenanceRateService);
var penaltyService = new PenaltyService(db);
var otherChargeService = new OtherChargeService(db);
var passed = 0;
async Task Test(string name, Func<Task> action)
{
    failure.Enabled = false;
    db.ChangeTracker.Clear();
    foreach (var table in tables)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM `{table}`";
        await command.ExecuteNonQueryAsync();
    }
    var now = DateTime.Now;
    db.Customers.Add(new Customer
    {
        Id = 1, Firstname = "Senior", Middlename = "Citizen", Lastname = "Tester",
        PhoneNumber = "09000000000", Email = "senior@example.invalid", IsActive = true,
        DateCreated = now, DateUpdated = now
    });
    db.Accounts.Add(new Account { Id = 1, CustomerId = 1, AccountNumber = "TEST", ZoneBookId = 1, Classification = 1, MeterSize = .5m, DateCreated = now, DateUpdated = now });
    db.ZoneBooks.Add(new ZoneBook { Id = 1, Zone = 1, Book = 1, Area = "Test Area", Week = 1 });
    db.Inventories.Add(new Inventory { Id = 1, Name = "Test Meter Brand", Division = 1, UOM = "unit", Quantity = 1, UnitCost = 0, UnitSellingPrice = 0, IsActive = true, DateCreated = now, DateUpdated = now });
    db.InspectionTypes.Add(new InspectionType { Id = 1, Name = "Service Request", IsActive = true, DateCreated = now, DateUpdated = now });
    db.ReadingSheets.Add(new ReadingSheet { Id = 1, ZoneBookId = 1, Status = ReadingStatus.InProgress, BillingDate = now.AddDays(1), DueDate = now.AddDays(10), BillingPeriodStart = now });
    db.OtherFeeTypes.Add(new OtherFeeType { Id = 1, Name = "Synthetic Other Fee", DateCreated = now, DateUpdate = now });
    db.WaterRates.Add(new WaterRate { Id = 1, Classification = 1, MeterSize = .5m, MinimumCharge = 100, Rate11To20 = 10, Rate21To30 = 20, Rate31To40 = 30, Rate41Up = 40, IsActive = true, EffectiveDate = now, DateCreated = now, DateUpdated = now });
    db.MeterMaintenanceRates.Add(new MeterMaintenanceRate { Id = 1, Amount = 20m,
        EffectiveFrom = new DateOnly(2025, 1, 1), CreatedBy = 3, UpdatedBy = 3,
        DateCreated = now, DateUpdated = now });
    db.BirInvoiceConfigs.Add(new BirInvoiceConfig { Id = 1, PTUNumber = "SYNTHETIC-PTU", PermitDateIssued = new DateOnly(2026, 1, 1),
        SeriesFrom = 1, SeriesTo = 999999, InvoicePrefix = "TEST-", SeriesWidth = 6, IsActive = true, DateCreated = now, DateUpdated = now });
    db.Users.AddRange(
        new User { Id = 3, Name = "Synthetic SuperAdmin", Email = "admin@test.invalid", Role = (int)UserRole.SuperAdmin,
            Password = "test", IsActive = true, IsVerified = true, DateCreated = now, DateUpdated = now },
        new User { Id = 1, Name = "Customer Service Representative", Email = "csr@test.invalid", Role = (int)UserRole.CustomerRepresentative, Password = "test", IsActive = true, IsVerified = true, DateVerified = now, DateCreated = now, DateUpdated = now },
        new User { Id = 2, Name = "Customer Service OIC", Email = "oic@test.invalid", Role = (int)UserRole.CustomerServiceOic, Password = "test", IsActive = true, IsVerified = true, DateVerified = now, DateCreated = now, DateUpdated = now });
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
    await action();
    Console.WriteLine($"PASS: {name}");
    passed++;
}
Reading NewReading(decimal current = 10, decimal previous = 0) => new() { AccountId = 1, ReadingSheetId = 1, CurrentReading = current, PreviousReading = previous, Status = ReadingStatus.InProgress, DateCreated = DateTime.Now };
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
async Task Reject(Func<Task> action)
{
    try { await action(); }
    catch (InvalidOperationException) { return; }
    throw new Exception("Expected an invalid-operation rejection.");
}
async Task RejectUnauthorized(Func<Task> action)
{
    try { await action(); }
    catch (UnauthorizedAccessException) { return; }
    throw new Exception("Expected a role-authorization rejection.");
}
async Task ExpectMissingMaintenanceRate(DateOnly billingDate)
{
    try { await maintenanceRateService.GetForBillingDate(billingDate); }
    catch (KeyNotFoundException) { return; }
    throw new Exception($"Expected no Meter Maintenance rate for {billingDate:yyyy-MM-dd}.");
}
await PostingSecurity.Run(db, sheetService, Test, enabled => failure.Enabled = enabled);
await Test("Meter Maintenance rate boundaries are selected by billing date", async () => {
    await ExpectMissingMaintenanceRate(new DateOnly(2024, 12, 31));
    foreach (var date in new[] { new DateOnly(2025, 1, 1), new DateOnly(2025, 7, 1),
        new DateOnly(2026, 1, 1), new DateOnly(2027, 6, 1) })
        Check((await maintenanceRateService.GetForBillingDate(date)).Amount == 20m,
            $"Approved 2025-01-01 initial rate did not cover {date:yyyy-MM-dd}.");
});
await Test("BillingDate 2024-12-31 blocks posting before bill or invoice allocation", async () => {
    var sheet = await db.ReadingSheets.SingleAsync();
    sheet.BillingDate = new DateTime(2024, 12, 31);
    sheet.BillingPeriodStart = new DateTime(2024, 12, 1);
    sheet.DueDate = new DateTime(2025, 1, 15);
    db.Readings.Add(new Reading { AccountId = 1, ReadingSheetId = 1, PreviousReading = 0,
        CurrentReading = 0, Status = ReadingStatus.InProgress, DateCreated = new DateTime(2024, 12, 30) });
    await db.SaveChangesAsync();
    try
    {
        await sheetService.PartialPost(1, 1);
    }
    catch (KeyNotFoundException)
    {
        Check(!await db.Billings.AnyAsync() && !await db.ServiceInvoiceSeq.AnyAsync(),
            "Missing pre-2025 configuration allocated a bill or invoice number.");
        Check((await db.Readings.SingleAsync()).Status == ReadingStatus.InProgress,
            "Missing pre-2025 configuration changed the pending reading.");
        return;
    }
    throw new Exception("A 2024-12-31 reading sheet posted without an approved historical rate.");
});
foreach (var date in new[] { new DateTime(2025, 1, 1), new DateTime(2025, 7, 1),
    new DateTime(2026, 1, 1), new DateTime(2027, 6, 1) })
await Test($"BillingDate {date:yyyy-MM-dd} posts the approved maintenance fee once", async () => {
    var sheet = await db.ReadingSheets.SingleAsync();
    sheet.BillingDate = date;
    sheet.BillingPeriodStart = date.AddMonths(-1);
    sheet.DueDate = date.AddDays(15);
    db.Readings.Add(new Reading { AccountId = 1, ReadingSheetId = 1, PreviousReading = 0,
        CurrentReading = 0, Status = ReadingStatus.InProgress, DateCreated = date.AddDays(-1) });
    await db.SaveChangesAsync();
    Check(await sheetService.PartialPost(1, 1) == 1, "An approved covered billing date did not post.");
    var bill = await db.Billings.SingleAsync();
    var snapshot = JsonSerializer.Deserialize<TMCWD.Model.Billing.OfficialInvoiceSnapshot>(bill.InvoiceSnapshotJson!)!;
    Check(bill.TotalBillAmount == 120m && bill.RemainingAmount == 120m
        && snapshot.BillAmount == 100m && snapshot.MeterMaintenance == 20m
        && snapshot.TotalDue == 120m && snapshot.OtherCharges == 0m,
        "The approved fee was omitted, duplicated, or mixed into water/Other Charges.");
});
await Test("Meter Maintenance future schedule switches at its inclusive effective date", async () => {
    var sheet = await db.ReadingSheets.SingleAsync();
    sheet.BillingDate = new DateTime(2026, 6, 1);
    sheet.BillingPeriodStart = new DateTime(2026, 5, 1);
    sheet.DueDate = new DateTime(2026, 6, 16);
    db.Readings.Add(new Reading { AccountId = 1, ReadingSheetId = 1, PreviousReading = 0,
        CurrentReading = 0, Status = ReadingStatus.InProgress, DateCreated = new DateTime(2026, 5, 31) });
    await db.SaveChangesAsync();
    Check(await sheetService.PartialPost(1, 1) == 1, "The posted 2026 bill fixture did not post.");
    var posted = await db.Billings.SingleAsync();
    var frozenSnapshot = posted.InvoiceSnapshotJson;
    var current = await db.MeterMaintenanceRates.SingleAsync();
    current.EffectiveTo = new DateOnly(2026, 12, 31);
    await maintenanceRateService.SaveUpdate(3, current);
    await maintenanceRateService.SaveUpdate(3, new MeterMaintenanceRate {
        Amount = 25m, EffectiveFrom = new DateOnly(2027, 1, 1)
    });
    Check((await maintenanceRateService.GetForBillingDate(new DateOnly(2026, 12, 31))).Amount == 20m,
        "The current rate ended before its inclusive EffectiveTo date.");
    Check((await maintenanceRateService.GetForBillingDate(new DateOnly(2027, 1, 1))).Amount == 25m,
        "Future rate was not selected on its EffectiveFrom date.");
    Check((await db.Billings.SingleAsync()).InvoiceSnapshotJson == frozenSnapshot,
        "Changing a future rate schedule rewrote an already-issued invoice snapshot.");
});
await Test("Meter Maintenance overlapping configurations fail closed", async () => {
    db.MeterMaintenanceRates.Add(new MeterMaintenanceRate { Id = 2, Amount = 30m,
        EffectiveFrom = new DateOnly(2026, 6, 1), EffectiveTo = new DateOnly(2026, 6, 30),
        CreatedBy = 3, UpdatedBy = 3, DateCreated = DateTime.Now, DateUpdated = DateTime.Now });
    await db.SaveChangesAsync();
    try
    {
        await maintenanceRateService.GetForBillingDate(new DateOnly(2026, 6, 15));
    }
    catch (InvalidOperationException)
    {
        return;
    }
    throw new Exception("Overlapping Meter Maintenance rates were not rejected.");
});
await Test("Only verified SuperAdmin may change Meter Maintenance configuration", async () => {
    await RejectUnauthorized(async () => await maintenanceRateService.SaveUpdate(1,
        new MeterMaintenanceRate { Amount = 25m, EffectiveFrom = new DateOnly(2027, 1, 1) }));
});
await Test("Reading Sheet detail returns 41 roster accounts with four saved and 37 pending readings", async () => {
    var sheet = await db.ReadingSheets.SingleAsync();
    sheet.SeqFrom = 1;
    sheet.SeqTo = 41;
    var first = await db.Accounts.SingleAsync();
    first.Status = (int)AccountStatus.Active;
    first.Sequence = 1;
    first.AccountNumber = "TEST-01";
    for (var i = 2; i <= 41; i++)
        db.Accounts.Add(new Account { Id = i, CustomerId = 1, AccountNumber = $"TEST-{i:00}",
            ZoneBookId = 1, Sequence = i, Status = (int)AccountStatus.Active,
            Classification = 1, MeterSize = .5m, DateCreated = DateTime.Now, DateUpdated = DateTime.Now });
    db.Accounts.AddRange(
        new Account { Id = 42, CustomerId = 1, AccountNumber = "OUT-OF-RANGE", ZoneBookId = 1, Sequence = 42, Status = (int)AccountStatus.Active },
        new Account { Id = 43, CustomerId = 1, AccountNumber = "INACTIVE", ZoneBookId = 1, Sequence = 10, Status = (int)AccountStatus.Closed },
        new Account { Id = 44, CustomerId = 1, AccountNumber = "OTHER-ZONE", ZoneBookId = 2, Sequence = 10, Status = (int)AccountStatus.Active });
    for (var i = 1; i <= 4; i++)
        db.Readings.Add(new Reading { AccountId = i, ReadingSheetId = 1,
            PreviousReading = i == 1 ? 0 : i * 10, CurrentReading = i == 1 ? 0 : i * 10 + 5,
            Status = ReadingStatus.Completed, IsCompleted = true, DateCreated = DateTime.Now });
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
    Check((await new AccountService(db).GetReadingSheetRoster(sheet)).Count == 41,
        "Shared roster must select 41 active accounts within the sheet zone/book/sequence.");
    Check(await db.Readings.CountAsync() == 4, "Fixture must contain only four saved readings.");
    var controller = new TMCWD.Data.Controllers.ReadingSheetController(sheetService);
    var response = await controller.GetReadingSheetAccounts(1) as Microsoft.AspNetCore.Mvc.OkObjectResult;
    Check(response != null, "Detail endpoint must succeed.");
    var rows = (List<TMCWD.Model.Billing.Responses.ReadingSheetAccountDto>)response!.Value!;
    Check(rows.Count == 41 && rows.Count(r => r.Status == "Pending" && r.Pres == null && r.Usage == null && r.ReadingDate == null) == 37,
        "Detail response must include 37 explicitly pending rows, without fabricated zero readings.");
    for (var i = 1; i <= 4; i++)
    {
        var row = rows.Single(r => r.Number == $"TEST-{i:00}");
        Check(row.Prev == (i == 1 ? 0 : i * 10) && row.Pres == (i == 1 ? 0 : i * 10 + 5)
            && row.Usage == (i == 1 ? 0 : 5) && row.Status == "Completed",
            "Saved values and zero-valued completed readings must be preserved.");
    }
    var json = JsonSerializer.SerializeToElement(rows, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    Check(json.GetArrayLength() == 41 && json.EnumerateArray().Count(r => r.GetProperty("pres").ValueKind == JsonValueKind.Null) == 37,
        "Serialized modal contract must preserve pending nulls.");
});
await Test("Sheet-relative previous reading ignores newer, same-period and deleted history", async () => {
    var target = await db.ReadingSheets.SingleAsync();
    target.Status = ReadingStatus.Completed;
    target.BillingDate = new DateTime(2026, 2, 1);
    db.ReadingSheets.AddRange(
        new ReadingSheet { Id = 2, ZoneBookId = 1, BillingDate = new DateTime(2026, 1, 1), Status = ReadingStatus.Completed },
        new ReadingSheet { Id = 3, ZoneBookId = 1, BillingDate = new DateTime(2026, 3, 1), Status = ReadingStatus.Completed },
        new ReadingSheet { Id = 4, ZoneBookId = 1, BillingDate = new DateTime(2026, 2, 1), Status = ReadingStatus.Completed },
        new ReadingSheet { Id = 5, ZoneBookId = 1, BillingDate = new DateTime(2026, 1, 25), Status = ReadingStatus.Deleted },
        new ReadingSheet { Id = 6, ZoneBookId = 1, BillingDate = new DateTime(2026, 1, 20), Status = ReadingStatus.Completed });
    db.Readings.AddRange(
        new Reading { Id = 10, AccountId = 1, ReadingSheetId = 2, CurrentReading = 40, Status = ReadingStatus.Completed, DateCreated = new DateTime(2025, 12, 31) },
        new Reading { Id = 11, AccountId = 1, ReadingSheetId = 1, PreviousReading = 40, CurrentReading = 50, Status = ReadingStatus.Completed, DateCreated = new DateTime(2026, 1, 31) },
        new Reading { Id = 12, AccountId = 1, ReadingSheetId = 3, CurrentReading = 80, Status = ReadingStatus.Completed, DateCreated = new DateTime(2026, 2, 28) },
        new Reading { Id = 13, AccountId = 1, ReadingSheetId = 4, CurrentReading = 90, Status = ReadingStatus.Completed, DateCreated = new DateTime(2026, 1, 31) },
        new Reading { Id = 14, AccountId = 1, ReadingSheetId = 5, CurrentReading = 95, Status = ReadingStatus.Completed, DateCreated = new DateTime(2026, 1, 24) },
        new Reading { Id = 15, AccountId = 1, ReadingSheetId = 6, CurrentReading = 99, Status = ReadingStatus.Deleted, DateCreated = new DateTime(2026, 1, 19) });
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
    var previous = await readingService.GetPreviousForSheet(1, 1);
    Check(previous?.Id == 10 && previous.CurrentReading == 40
        && previous.DateCreated == new DateTime(2025, 12, 31),
        "Older completed sheet must resolve its actual earlier-period value/date, ignoring future/same-period/deleted records.");
    Check(await readingService.GetPreviousForSheet(1, 2) == null,
        "First historical sheet must not receive a future reading.");
    Check(await readingService.GetPreviousForSheet(999, 1) == null,
        "An account without history must have no fabricated previous reading.");
});
await Test("Mobile sync upserts one sheet/account and preserves remarks across retries and posting", async () => {
    var sheet = await db.ReadingSheets.SingleAsync(); sheet.AssignedTo = 10;
    var account = await db.Accounts.SingleAsync(); account.Status = (int)AccountStatus.Active;
    await db.SaveChangesAsync();
    var upload = new TMCWD.Model.Billing.Requests.MobileReadingUpload { AccountId = 1, CurrentReading = 0,
        Remarks = "Synthetic remark", ReadingDate = DateTime.Now };
    var first = await readingService.Sync(10, 1, upload);
    db.ChangeTracker.Clear();
    var again = await readingService.Sync(10, 1, upload);
    Check(first.Id == again.Id && await db.Readings.CountAsync() == 1 && again.Remarks == upload.Remarks && again.CurrentReading == 0,
        "Retry duplicated or changed reading/remarks/zero.");
    upload.CurrentReading = 5.25m;
    await readingService.Sync(10, 1, upload);
    db.ChangeTracker.Clear();
    Check(await db.Readings.CountAsync() == 1 && (await db.Readings.SingleAsync()).CurrentReading == 5.25m, "Edit did not upsert or decimal was rounded.");
    Check(await sheetService.PartialPost(1, 1) == 1, "Fixture did not lock reading.");
    db.ChangeTracker.Clear();
    await readingService.Sync(10, 1, upload);
    upload.CurrentReading = 6;
    await Reject(async () => await readingService.Sync(10, 1, upload));
    db.ChangeTracker.Clear();
    Check((await db.Readings.SingleAsync()).CurrentReading == 5.25m, "Locked reading was overwritten.");
});
await Test("Mobile sync enforces assignment, authoritative roster, dates and previous reading", async () => {
    var sheet = await db.ReadingSheets.SingleAsync(); sheet.AssignedTo = 10;
    var account = await db.Accounts.SingleAsync(); account.Status = (int)AccountStatus.Active; account.Sequence = 5;
    sheet.SeqFrom = 5; sheet.SeqTo = 5;
    await db.SaveChangesAsync();
    var upload = new TMCWD.Model.Billing.Requests.MobileReadingUpload { AccountId = 1, CurrentReading = 10, ReadingDate = DateTime.Now };
    await RejectUnauthorized(async () => await readingService.Sync(11, 1, upload));
    async Task Bad() { try { await readingService.Sync(10, 1, upload); } catch (ArgumentException) { return; } throw new Exception("Invalid mobile upload accepted."); }
    account.Sequence = 6; await db.SaveChangesAsync(); await Bad();
    account.Sequence = 5; account.Status = (int)AccountStatus.Suspended; await db.SaveChangesAsync(); await Bad();
    account.Status = (int)AccountStatus.Active; await db.SaveChangesAsync();
    upload.CurrentReading = -1; await Bad();
    upload.CurrentReading = 10; upload.ReadingDate = sheet.BillingDate!.Value; await Bad();
    upload.ReadingDate = DateTime.Now; await readingService.Sync(10, 1, upload);
    var stored = await db.Readings.SingleAsync(); stored.PreviousReading = 8; await db.SaveChangesAsync();
    upload.CurrentReading = 7; await Reject(async () => await readingService.Sync(10, 1, upload));
    db.ChangeTracker.Clear(); Check((await db.Readings.SingleAsync()).CurrentReading == 10, "Rejected upload changed reading.");
});
await Test("Synced mobile readings stay InProgress until web posting and final completion", async () => {
    var sheet = await db.ReadingSheets.SingleAsync(); sheet.AssignedTo = 10;
    var account = await db.Accounts.SingleAsync(); account.Status = (int)AccountStatus.Active;
    db.Accounts.Add(new Account { Id = 2, CustomerId = 1, AccountNumber = "NEW-ROSTER", ZoneBookId = 1,
        Status = (int)AccountStatus.Active, Classification = 1, MeterSize = .5m, DateCreated = DateTime.Now });
    await db.SaveChangesAsync();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 0, Remarks = "Zero", ReadingDate = DateTime.Now });
    await readingService.Sync(10, 1, new() { AccountId = 2, CurrentReading = 5, Remarks = "Normal", ReadingDate = DateTime.Now });
    db.ChangeTracker.Clear();
    Check((await db.ReadingSheets.SingleAsync()).Status == ReadingStatus.InProgress
        && (await db.ReadingSheets.SingleAsync()).DateUpload == null, "Sync prematurely completed the sheet.");
    Check(await db.Readings.CountAsync() == 2 && await db.Billings.CountAsync() == 0
        && await db.Readings.AllAsync(r => !r.IsCompleted && r.Status == ReadingStatus.InProgress),
        "Sync must not bill or mark readings posted/completed.");
    Check(await sheetService.CompleteReadingSheet(1, 1) == null, "Web completion accepted unposted readings.");
    Check(await sheetService.PartialPost(1, 1) == 2, "Web PartialPost rejected synced readings.");
    db.ChangeTracker.Clear();
    Check(await db.Billings.CountAsync() == 2 && await db.Readings.AllAsync(r => r.IsCompleted && r.Status == ReadingStatus.Completed),
        "Web posting did not produce bills and complete readings.");
    Check((await db.ReadingSheets.SingleAsync()).Status == ReadingStatus.InProgress, "PartialPost prematurely completed the sheet.");
    db.Accounts.Add(new Account { Id = 3, CustomerId = 1, AccountNumber = "ACTIVATED-AFTER-POST", ZoneBookId = 1,
        Status = (int)AccountStatus.Active, Classification = 1, MeterSize = .5m, DateCreated = DateTime.Now });
    await db.SaveChangesAsync();
    Check(await sheetService.CompleteReadingSheet(1, 1) == null, "Web completion ignored a newly activated unread roster account.");
    await readingService.Sync(10, 1, new() { AccountId = 3, CurrentReading = 0, ReadingDate = DateTime.Now });
    Check(await sheetService.PartialPost(1, 1) == 1, "New roster reading could not be posted.");
    var completed = await sheetService.CompleteReadingSheet(1, 1);
    Check(completed?.Status == ReadingStatus.Completed && completed.DateUpload != null,
        "Only web final completion should complete the reading sheet.");
});
await Test("Prior unpaid and overdue bills populate authoritative arrears from prior snapshots", async () => {
    var sheet = await db.ReadingSheets.SingleAsync();
    sheet.BillingDate = DateTime.Today;
    var account = await db.Accounts.SingleAsync();
    account.Status = (int)AccountStatus.Active;
    account.Sequence = 1;
    var priorBill = new Billing
    {
        AccountId = 1,
        ReadingId = 999,
        BillingReferenceId = "ARREARS-VALID",
        BillingPeriod = DateTime.Today.AddDays(-5),
        TotalBillAmount = 190m,
        RemainingAmount = 190m,
        PaymentStatus = (int)PaymentStatus.Unpaid,
        InvoiceSnapshotJson = JsonSerializer.Serialize(new TMCWD.Model.Billing.OfficialInvoiceSnapshot(
            SchemaVersion: 1,
            PeriodFrom: DateTime.Today.AddDays(-10),
            PeriodTo: DateTime.Today.AddDays(-5),
            BillAmount: 170m,
            MeterMaintenance: 20m,
            PowerCostAdjustment: null,
            ArrearsAmount: null,
            OtherCharges: 0m,
            BillingAdjustment: 0m,
            TotalDue: 190m,
            ChargeLines: Array.Empty<TMCWD.Model.Billing.InvoiceChargeLine>())),
        CreatedBy = 1,
        UpdatedBy = 1,
        DateCreated = DateTime.Now,
        DateUpdated = DateTime.Now
    };
    db.Billings.Add(priorBill);
    db.Penalties.Add(new Penalty
    {
        BillingReferenceId = priorBill.BillingReferenceId,
        Amount = 17m,
        PaymentStatus = (int)PaymentStatus.Unpaid,
        PenaltyTypeId = 1,
        CreatedBy = 1,
        UpdatedBy = 1,
        DateCreated = DateTime.Now,
        DateUpdated = DateTime.Now
    });
    await db.SaveChangesAsync();
    var reading = await readingService.SaveUpdate(1, NewReading(15, 0));
    Check(reading != null, "Synthetic reading was not saved.");
    Check(await sheetService.PartialPost(1, 1) == 1, "Prior invoice snapshot arrears bill was not posted.");
    db.ChangeTracker.Clear();
    var billing = await db.Billings.OrderByDescending(x => x.Id).FirstAsync();
    Check(billing.BillArrearsAmount == 207m, "Prior unpaid bill did not include maintenance and active prior penalties in the authoritative arrears amount.");
    var snapshot = JsonSerializer.Deserialize<TMCWD.Model.Billing.OfficialInvoiceSnapshot>(billing.InvoiceSnapshotJson!);
    Check(snapshot != null && snapshot.ArrearsAmount == 207m,
        "Official invoice snapshot did not freeze prior arrears including maintenance and active penalties.");
});
await Test("Arrears use outstanding prior bill balances, active penalties, and preserve snapshots", async () => {
    var sheet = await db.ReadingSheets.SingleAsync();
    sheet.BillingDate = new DateTime(2026, 12, 1);
    var account = await db.Accounts.SingleAsync();
    account.Status = (int)AccountStatus.Active;
    account.Sequence = 1;
    var date = new DateTime(2026, 10, 1);
    string Snapshot(decimal water, decimal maintenance = 20m, decimal? arrears = null, decimal? totalDue = null) =>
        JsonSerializer.Serialize(new TMCWD.Model.Billing.OfficialInvoiceSnapshot(
            SchemaVersion: 1,
            PeriodFrom: date.AddMonths(-1),
            PeriodTo: date,
            BillAmount: water,
            MeterMaintenance: maintenance,
            PowerCostAdjustment: null,
            ArrearsAmount: arrears,
            OtherCharges: 0m,
            BillingAdjustment: 0m,
            TotalDue: totalDue ?? water + maintenance,
            ChargeLines: Array.Empty<TMCWD.Model.Billing.InvoiceChargeLine>()));
    Billing PriorBill(string reference, DateTime period, decimal remaining, string snapshot,
        int status = (int)PaymentStatus.Unpaid) => new()
    {
        AccountId = 1,
        ReadingId = 900,
        BillingReferenceId = reference,
        BillingPeriod = period,
        TotalBillAmount = 120m,
        RemainingAmount = remaining,
        PaymentStatus = status,
        InvoiceSnapshotJson = snapshot,
        CreatedBy = 1,
        UpdatedBy = 1,
        DateCreated = date,
        DateUpdated = date
    };

    var unadjusted = PriorBill("ARREARS-UNADJUSTED", new DateTime(2026, 11, 1), 120m,
        Snapshot(100m));
    var adjusted = PriorBill("ARREARS-ADJUSTED", new DateTime(2026, 10, 1), 120m,
        Snapshot(100m, arrears: 500m, totalDue: 620m), (int)PaymentStatus.Overdue);
    var multiplePeriod = PriorBill("ARREARS-MULTI-PERIOD", new DateTime(2026, 9, 1), 120m,
        Snapshot(100m));
    var paid = PriorBill("ARREARS-PAID", new DateTime(2026, 8, 1), 0m,
        Snapshot(100m), (int)PaymentStatus.Paid);
    var zeroBalance = PriorBill("ARREARS-ZERO", new DateTime(2026, 7, 1), 0m,
        Snapshot(100m));
    var cancelled = PriorBill("ARREARS-CANCELLED", new DateTime(2026, 6, 1), 120m,
        Snapshot(100m));
    var samePeriod = PriorBill("ARREARS-SAME-PERIOD", new DateTime(2026, 12, 1), 25m,
        Snapshot(100m));
    var futurePeriod = PriorBill("ARREARS-FUTURE-PERIOD", new DateTime(2027, 1, 1), 35m,
        Snapshot(100m));
    var otherAccount = PriorBill("ARREARS-OTHER-ACCOUNT", new DateTime(2026, 5, 1), 45m,
        Snapshot(100m));
    otherAccount.AccountId = 9999;
    db.Billings.AddRange(unadjusted, adjusted, multiplePeriod, paid, zeroBalance, cancelled,
        samePeriod, futurePeriod, otherAccount);
    await db.SaveChangesAsync();
    var frozenSnapshots = new Dictionary<string, string>
    {
        [unadjusted.BillingReferenceId] = unadjusted.InvoiceSnapshotJson!,
        [adjusted.BillingReferenceId] = adjusted.InvoiceSnapshotJson!,
        [multiplePeriod.BillingReferenceId] = multiplePeriod.InvoiceSnapshotJson!,
        [paid.BillingReferenceId] = paid.InvoiceSnapshotJson!,
        [zeroBalance.BillingReferenceId] = zeroBalance.InvoiceSnapshotJson!,
        [cancelled.BillingReferenceId] = cancelled.InvoiceSnapshotJson!,
        [samePeriod.BillingReferenceId] = samePeriod.InvoiceSnapshotJson!,
        [futurePeriod.BillingReferenceId] = futurePeriod.InvoiceSnapshotJson!,
        [otherAccount.BillingReferenceId] = otherAccount.InvoiceSnapshotJson!
    };

    var adjustments = new BillingAdjustmentService(db);
    await adjustments.SaveBatch(1, new()
    {
        new BillingAdjustment
        {
            BillingReferenceId = adjusted.BillingReferenceId,
            Type = (int)AdjustmentLineType.CurrentBill,
            Amount = 30m
        }
    }, Guid.NewGuid());
    await adjustments.SaveBatch(1, new()
    {
        new BillingAdjustment
        {
            BillingReferenceId = multiplePeriod.BillingReferenceId,
            Type = (int)AdjustmentLineType.CurrentBill,
            Amount = 70m
        }
    }, Guid.NewGuid());
    db.Penalties.AddRange(
        new Penalty
        {
            BillingReferenceId = adjusted.BillingReferenceId,
            Amount = 17m,
            PaymentStatus = (int)PaymentStatus.Unpaid,
            PenaltyTypeId = 1,
            CreatedBy = 1,
            UpdatedBy = 1,
            DateCreated = date,
            DateUpdated = date
        },
        new Penalty
        {
            BillingReferenceId = paid.BillingReferenceId,
            Amount = 10m,
            PaymentStatus = (int)PaymentStatus.Paid,
            PenaltyTypeId = 1,
            CreatedBy = 1,
            UpdatedBy = 1,
            DateCreated = date,
            DateUpdated = date
        });
    var otherCharge = new OtherCharge
    {
        BillingReferenceId = adjusted.BillingReferenceId,
        Type = 1,
        Amount = 500m,
        PayableIn = "1",
        InstallmentCount = 1,
        PaymentStatus = (int)PaymentStatus.Unpaid,
        IsActive = true,
        CreatedBy = 1,
        UpdatedBy = 1,
        DateCreated = date,
        DateUpdated = date
    };
    db.OtherCharges.Add(otherCharge);
    await db.SaveChangesAsync();
    db.OtherChargeInstallments.Add(new OtherChargeInstallment
    {
        OtherChargeId = otherCharge.Id,
        InstallmentNumber = 1,
        DueBillingPeriod = new DateTime(2026, 12, 1),
        Amount = 500m,
        PaymentStatus = (int)PaymentStatus.Unpaid,
        CreatedBy = 1,
        UpdatedBy = 1,
        DateCreated = date,
        DateUpdated = date
    });
    await db.SaveChangesAsync();
    Check((await db.Billings.SingleAsync(b => b.BillingReferenceId == adjusted.BillingReferenceId)).RemainingAmount == 90m
        && (await db.Billings.SingleAsync(b => b.BillingReferenceId == multiplePeriod.BillingReferenceId)).RemainingAmount == 50m,
        "Prior adjustments did not establish the expected authoritative outstanding balances.");

    var payments = new PaymentService(db);
    var receipt = (await payments.ProcessCollectionPayment(1, new()
    {
        Method = PaymentMethod.Cash,
        AmountTendered = 120m,
        Bills = new() { new() { BillingReferenceId = cancelled.BillingReferenceId, PaidAmount = 120m } }
    })).Single().PaymentReference;
    await payments.CancelInvoice(1, new TMCWD.Model.Billing.CancelInvoiceRequest
    {
        InvoiceNo = receipt,
        Reason = "Synthetic arrears regression"
    });
    db.ChangeTracker.Clear();
    Check((await db.Billings.SingleAsync(b => b.BillingReferenceId == cancelled.BillingReferenceId)).RemainingAmount == 120m
        && (await db.Billings.SingleAsync(b => b.BillingReferenceId == cancelled.BillingReferenceId)).PaymentStatus == (int)PaymentStatus.Unpaid,
        "Cancelled payment did not restore the qualifying prior bill balance.");

    await readingService.SaveUpdate(1, NewReading(15, 0));
    Check(await sheetService.PartialPost(1, 1) == 1, "Current bill was not posted with prior arrears.");
    db.ChangeTracker.Clear();
    var current = await db.Billings.SingleAsync(b => b.ReadingId != 900);
    Check(current.BillArrearsAmount == 397m,
        $"Expected 120 unadjusted + 90 adjusted + 17 active penalty + 50 adjusted older + 120 cancelled = 397; got {current.BillArrearsAmount}.");
    var currentSnapshot = JsonSerializer.Deserialize<TMCWD.Model.Billing.OfficialInvoiceSnapshot>(current.InvoiceSnapshotJson!)!;
    Check(currentSnapshot.ArrearsAmount == 397m,
        "The new invoice snapshot did not freeze the non-recursive outstanding prior principal and active penalties.");
    foreach (var (reference, frozen) in frozenSnapshots)
        Check((await db.Billings.AsNoTracking().SingleAsync(b => b.BillingReferenceId == reference)).InvoiceSnapshotJson == frozen,
            $"Previously issued snapshot {reference} was changed.");
    Check(await db.OtherChargeInstallments.CountAsync() == 1,
        "The future Other Charge installment fixture was unexpectedly changed.");
});
await Test("Missing eligible prior invoice snapshot leaves arrears unknown", async () => {
    var sheet = await db.ReadingSheets.SingleAsync();
    sheet.BillingDate = DateTime.Today;
    var account = await db.Accounts.SingleAsync();
    account.Status = (int)AccountStatus.Active;
    account.Sequence = 1;
    db.Billings.Add(new Billing
    {
        AccountId = 1,
        ReadingId = 1001,
        BillingReferenceId = "ARREARS-MISSING-SNAPSHOT",
        BillingPeriod = DateTime.Today.AddDays(-2),
        TotalBillAmount = 120m,
        RemainingAmount = 90m,
        BillingAdjustment = 30m,
        PaymentStatus = (int)PaymentStatus.Unpaid,
        InvoiceSnapshotJson = null,
        CreatedBy = 1,
        UpdatedBy = 1,
        DateCreated = DateTime.Now,
        DateUpdated = DateTime.Now
    });
    await db.SaveChangesAsync();
    await readingService.SaveUpdate(1, NewReading(20, 0));
    Check(await sheetService.PartialPost(1, 1) == 1, "Posting should succeed with unknown prior arrears.");
    db.ChangeTracker.Clear();
    var billing = await db.Billings.OrderByDescending(x => x.Id).FirstAsync();
    Check(billing.BillArrearsAmount == null,
        "Missing authoritative snapshot must leave arrears unknown, not infer it from RemainingAmount.");
    var snapshot = JsonSerializer.Deserialize<TMCWD.Model.Billing.OfficialInvoiceSnapshot>(billing.InvoiceSnapshotJson!);
    Check(snapshot?.ArrearsAmount == null,
        "The issued snapshot must preserve unknown arrears instead of assuming zero.");
});
await Test("Malformed eligible prior bills force arrears to null instead of guessing", async () => {
    var sheet = await db.ReadingSheets.SingleAsync();
    sheet.BillingDate = DateTime.Today;
    var account = await db.Accounts.SingleAsync();
    account.Status = (int)AccountStatus.Active;
    account.Sequence = 1;
    db.Billings.Add(new Billing
    {
        AccountId = 1,
        ReadingId = 1000,
        BillingReferenceId = "ARREARS-INVALID",
        BillingPeriod = DateTime.Today.AddDays(-2),
        TotalBillAmount = 200m,
        RemainingAmount = 200m,
        PaymentStatus = (int)PaymentStatus.Overdue,
        InvoiceSnapshotJson = "{ not valid json }",
        CreatedBy = 1,
        UpdatedBy = 1,
        DateCreated = DateTime.Now,
        DateUpdated = DateTime.Now
    });
    await db.SaveChangesAsync();
    var reading = await readingService.SaveUpdate(1, NewReading(20, 0));
    Check(reading != null, "Synthetic reading for invalid arrears was not saved.");
    Check(await sheetService.PartialPost(1, 1) == 1, "Posting should still succeed but leave arrears null when the prior snapshot is malformed.");
    db.ChangeTracker.Clear();
    var billing = await db.Billings.OrderByDescending(x => x.Id).FirstAsync();
    Check(billing.BillArrearsAmount == null, "Malformed prior snapshot must force arrears to null.");
    var snapshot = JsonSerializer.Deserialize<TMCWD.Model.Billing.OfficialInvoiceSnapshot>(billing.InvoiceSnapshotJson!);
    Check(snapshot != null && snapshot.ArrearsAmount == null, "Malformed prior snapshot must not populate the official invoice snapshot arrears.");
});
await Test("Official mobile SOA reads persisted partial-post billing without mutations or rate recalculation", async () => {
    var sheet = await db.ReadingSheets.SingleAsync();
    sheet.AssignedTo = 10; sheet.SeqFrom = 1; sheet.SeqTo = 20;
    var account = await db.Accounts.SingleAsync();
    account.Status = (int)AccountStatus.Active; account.Sequence = 1;
    account.MeterNumber = "SOA-METER"; account.Street = "Synthetic Street";
    db.Accounts.Add(new Account { Id = 2, CustomerId = 1, AccountNumber = "UNREAD-SOA", ZoneBookId = 1,
        Sequence = 2, Status = (int)AccountStatus.Active, Classification = 1, MeterSize = .5m, DateCreated = DateTime.Now });
    db.Accounts.Add(new Account { Id = 3, CustomerId = 1, AccountNumber = "OUTSIDE-SOA", ZoneBookId = 1,
        Sequence = 21, Status = (int)AccountStatus.Active, Classification = 1, MeterSize = .5m, DateCreated = DateTime.Now });
    db.Users.Add(new User { Id = 10, Name = "Synthetic Reader", Email = "reader@test.invalid", Password = "synthetic",
        Role = (int)UserRole.MeterReader, IsActive = true, IsVerified = true, DateCreated = DateTime.Now, DateUpdated = DateTime.Now });
    await db.SaveChangesAsync();
    var controller = new TMCWD.Data.Controllers.MobileReadingSheetController(sheetService, new UserService(db),
        new AccountService(db), readingService, new ZoneBookService(db), new CustomerService(db), new BillingService(db))
    {
        ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                    [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "10")], "test"))
            }
        }
    };
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 15, Remarks = "Normal Reading", ReadingDate = DateTime.Now });
    Check(!await db.Billings.AnyAsync(), "Upload created a bill.");
    var unavailable = await controller.Soa(1, 1);
    Check(unavailable.Result is Microsoft.AspNetCore.Mvc.ConflictObjectResult conflict
        && JsonSerializer.Serialize(conflict.Value).Contains("not_posted"), "Synced but unposted reading exposed SOA.");
    Check((await controller.Soa(1, 2)).Result is Microsoft.AspNetCore.Mvc.ConflictObjectResult, "Unread roster account exposed SOA.");
    Check((await controller.Soa(1, 3)).Result is Microsoft.AspNetCore.Mvc.NotFoundResult, "Outside sequence account exposed SOA.");
    Check(await sheetService.PartialPost(1, 1) == 1, "Real PartialPost failed.");
    db.ChangeTracker.Clear();
    var billing = await db.Billings.SingleAsync();
    var persistedAmount = billing.TotalBillAmount;
    Check(persistedAmount == 170m, "Posting fixture amount changed.");
    var rate = await db.WaterRates.SingleAsync();
    rate.MinimumCharge = 9876m; rate.Rate11To20 = 999m; rate.IsActive = false;
    await db.SaveChangesAsync();

    async Task<string> Snapshot() => JsonSerializer.Serialize(new {
        Sheets = await db.ReadingSheets.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
        Readings = await db.Readings.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
        Bills = await db.Billings.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
        Payments = await db.Payments.AsNoTracking().OrderBy(x => x.Id).ToListAsync()
    });
    var before = await Snapshot();
    var first = ((await controller.Soa(1, 1)).Result as Microsoft.AspNetCore.Mvc.OkObjectResult)?.Value
        as TMCWD.Model.Billing.Responses.MobileSoaDto;
    Check(first != null && first.TotalBillAmount == persistedAmount && first.RemainingAmount == billing.RemainingAmount
        && first.BillingId == billing.Id && first.BillingReferenceId == billing.BillingReferenceId
        && first.CurrentReading == 15 && first.PreviousReading == 0 && first.Usage == 15
        && first.MeterNumber == "SOA-METER" && first.Address == "Synthetic Street"
        && first.PaymentStatus == PaymentStatus.Unpaid, "Persisted SOA amount/identity/display failed after rate change.");
    var second = ((await controller.Soa(1, 1)).Result as Microsoft.AspNetCore.Mvc.OkObjectResult)?.Value
        as TMCWD.Model.Billing.Responses.MobileSoaDto;
    var soaOptions = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
    Check(System.Text.Json.JsonSerializer.Serialize(first, soaOptions) == System.Text.Json.JsonSerializer.Serialize(second, soaOptions)
        && await Snapshot() == before, "Repeated SOA GET mutated billing, readings, sheets or payments.");
    Check((await db.ReadingSheets.SingleAsync()).Status == ReadingStatus.InProgress
        && (await db.ReadingSheets.SingleAsync()).DateUpload == null
        && (await db.Readings.SingleAsync()).Status == ReadingStatus.Completed
        && await db.Billings.CountAsync() == 1, "SOA changed partial-post lifecycle.");
    billing.PaymentStatus = (int)PaymentStatus.Paid; billing.RemainingAmount = 0;
    await db.SaveChangesAsync();
    before = await Snapshot();
    var paid = ((await controller.Soa(1, 1)).Result as Microsoft.AspNetCore.Mvc.OkObjectResult)?.Value
        as TMCWD.Model.Billing.Responses.MobileSoaDto;
    Check(paid?.PaymentStatus == PaymentStatus.Paid && paid.RemainingAmount == 0 && await Snapshot() == before,
        "SOA must report paid state without changing it.");
});
await Test("Repeated reading save leaves one record", async () => {
    await readingService.SaveUpdate(1, NewReading());
    await Reject(async () => await readingService.SaveUpdate(1, NewReading()));
    Check(await db.Readings.CountAsync() == 1, "Duplicate reading was stored.");
});
await Test("Negative and decreasing readings are rejected", async () => {
    await Reject(async () => await readingService.SaveUpdate(1, NewReading(-1)));
    await Reject(async () => await readingService.SaveUpdate(1, NewReading(5, 10)));
    Check(await db.Readings.CountAsync() == 0, "Invalid reading was stored.");
});
await Test("Zero reading posts the minimum bill once", async () => {
    await readingService.SaveUpdate(1, NewReading(0));
    Check(await sheetService.PartialPost(1, 1) == 1, "Zero reading not posted.");
    Check(await sheetService.PartialPost(1, 1) == 0, "Repeat posting was not idempotent.");
    var bill = await db.Billings.SingleAsync();
    Check(bill.TotalBillAmount == 120 && bill.RemainingAmount == 120, "Minimum plus maintenance charge is incorrect.");
    Check((await db.Readings.SingleAsync()).IsCompleted, "Reading completion flag missing.");
});
await Test("Legacy duplicate readings block posting", async () => {
    db.Readings.AddRange(NewReading(10), NewReading(20));
    await db.SaveChangesAsync();
    await Reject(async () => await sheetService.PartialPost(1, 1));
    Check(await db.Billings.CountAsync() == 0, "Duplicate account was billed.");
});
await Test("Existing bill is recovered without rebilling", async () => {
    var reading = await readingService.SaveUpdate(1, NewReading());
    db.Billings.Add(new Billing { AccountId = 1, ReadingId = reading.Id, BillingReferenceId = "EXISTING", BillingPeriod = DateTime.Today, DateCreated = DateTime.Now, DateUpdated = DateTime.Now });
    await db.SaveChangesAsync();
    Check(await sheetService.PartialPost(1, 1) == 0, "Created another bill.");
    Check(await db.Billings.CountAsync() == 1, "Existing reading was billed twice.");
    Check((await db.Readings.SingleAsync()).Status == ReadingStatus.Completed, "Existing bill not reconciled.");
});
await Test("Failure after SQL save rolls back bill and reading status", async () => {
    await readingService.SaveUpdate(1, NewReading());
    failure.Enabled = true;
    await Reject(async () => await sheetService.PartialPost(1, 1));
    failure.Enabled = false;
    db.ChangeTracker.Clear();
    Check(await db.Billings.CountAsync() == 0, "Failed transaction left a bill behind.");
    Check((await db.Readings.SingleAsync()).Status == ReadingStatus.InProgress, "Failed transaction changed reading status.");
});
await Test("CSR request leaves penalties active until OIC approval", async () => {
    foreach (var status in new[] { PaymentStatus.Unpaid, PaymentStatus.Overdue, PaymentStatus.Paid, PaymentStatus.Waived })
        db.Penalties.Add(new Penalty { BillingReferenceId = "TEST", PaymentStatus = (int)status, Amount = 10, DateCreated = DateTime.Now, DateUpdated = DateTime.Now });
    await db.SaveChangesAsync();
    Check(await penaltyService.SubmitWaiverRequests(1, new() { BillingReferenceIds = new() { "TEST" }, Reason = "Documented customer hardship" }) == 1,
        "Waiver request was not submitted.");
    Check(await db.Penalties.CountAsync(p => p.PaymentStatus == (int)PaymentStatus.Unpaid || p.PaymentStatus == (int)PaymentStatus.Overdue) == 2,
        "Submission waived a penalty before OIC approval.");
    var waiver = await db.PenaltyWaiverRequests.SingleAsync();
    Check((await penaltyService.GetPendingWaiverRequests(2)).Single().Reason == "Documented customer hardship",
        "OIC could not retrieve the pending waiver request.");
    await RejectUnauthorized(async () => await penaltyService.ReviewWaiverRequest(1,
        new() { RequestId = waiver.Id, Approve = true }));
    Check(await penaltyService.ReviewWaiverRequest(2, new() { RequestId = waiver.Id, Approve = true, Remarks = "Approved" }) == 2,
        "OIC approval did not waive the outstanding penalties.");
    db.ChangeTracker.Clear();
    Check(await db.Penalties.CountAsync(p => p.PaymentStatus == (int)PaymentStatus.Paid) == 1, "Paid penalty was overwritten.");
    Check((await db.PenaltyWaiverRequests.SingleAsync()).Status == (int)PenaltyWaiverRequestStatus.Approved,
        "Approved waiver request was not audited.");
});
await Test("Active penalties are listed by their exact due date", async () => {
    var selectedDueDate = DateTime.Today.AddDays(10);
    var reading = await readingService.SaveUpdate(1, NewReading(18, 3));
    db.Billings.Add(new Billing
    {
        AccountId = 1, ReadingId = reading.Id, BillingReferenceId = "DUE-DATE",
        BillingPeriod = DateTime.Today, TotalBillAmount = 500, RemainingAmount = 500,
        PaymentStatus = (int)PaymentStatus.Unpaid, DateCreated = DateTime.Now, DateUpdated = DateTime.Now
    });
    db.Penalties.AddRange(
        new Penalty { BillingReferenceId = "DUE-DATE", PaymentStatus = (int)PaymentStatus.Unpaid, Amount = 25, DateCreated = DateTime.Now, DateUpdated = DateTime.Now },
        new Penalty { BillingReferenceId = "DUE-DATE", PaymentStatus = (int)PaymentStatus.Overdue, Amount = 15, DateCreated = DateTime.Now, DateUpdated = DateTime.Now },
        new Penalty { BillingReferenceId = "DUE-DATE", PaymentStatus = (int)PaymentStatus.Paid, Amount = 99, DateCreated = DateTime.Now, DateUpdated = DateTime.Now });
    await db.SaveChangesAsync();

    var entry = (await penaltyService.GetActiveEntries(selectedDueDate)).Single();
    Check(entry.AccountNumber == "TEST" && entry.Usage == 15 && entry.Penalty == 40,
        "Due-date results did not aggregate active penalties correctly.");
    Check((await penaltyService.GetActiveEntries(selectedDueDate.AddDays(1))).Count == 0,
        "A penalty was returned for the wrong due date.");
});
async Task<Billing> OverdueBill()
{
    var sheet = await db.ReadingSheets.SingleAsync();
    sheet.DueDate = TMCWD.Model.Billing.LatePaymentPenalty.Today.AddDays(-1);
    await db.SaveChangesAsync();
    var reading = await readingService.SaveUpdate(1, NewReading());
    var bill = new Billing { AccountId = 1, ReadingId = reading.Id, BillingReferenceId = "OVERDUE",
        BillingPeriod = sheet.BillingDate!.Value, TotalBillAmount = 1000, RemainingAmount = 1000,
        InvoiceSnapshotJson = JsonSerializer.Serialize(new TMCWD.Model.Billing.OfficialInvoiceSnapshot(
            1, null, reading.DateCreated, 1000m, 20m, null, null, 0m, 0m, 1020m, Array.Empty<TMCWD.Model.Billing.InvoiceChargeLine>())),
        PaymentStatus = (int)PaymentStatus.Unpaid, DateCreated = DateTime.Now, DateUpdated = DateTime.Now };
    db.Billings.Add(bill);
    await db.SaveChangesAsync();
    return bill;
}
await Test("Penalty eligibility begins after the due date and excludes settled bills", async () => {
    var bill = await OverdueBill();
    Check((await penaltyService.GetChargeEntries(null)).Single().Penalty == 100, "Wrong penalty preview.");
    var sheet = await db.ReadingSheets.SingleAsync();
    foreach (var offset in new[] { 0, 1 }) {
        sheet.DueDate = TMCWD.Model.Billing.LatePaymentPenalty.Today.AddDays(offset);
        await db.SaveChangesAsync();
        Check((await penaltyService.GetChargeEntries(null)).Count == 0, "Not-yet-overdue bill was included.");
    }
    sheet.DueDate = TMCWD.Model.Billing.LatePaymentPenalty.Today.AddDays(-1);
    foreach (var status in new[] { PaymentStatus.Paid, PaymentStatus.Waived }) {
        bill.PaymentStatus = (int)status;
        await db.SaveChangesAsync();
        Check((await penaltyService.GetChargeEntries(null)).Count == 0, "Settled bill was included.");
    }
});
await Test("Posting saves 10 percent once, without changing the water bill", async () => {
    var bill = await OverdueBill();
    var request = new TMCWD.Model.Billing.PostPenaltyChargesRequest { BillingReferenceIds = new() { "OVERDUE" } };
    var result = await penaltyService.PostCharges(1, request);
    Check(result.PostedCount == 1 && result.TotalPenalty == 100, "Incorrect posting confirmation.");
    Check((await db.Penalties.SingleAsync()).Amount == 100 && bill.TotalBillAmount == 1000, "Penalty changed the bill basis.");
    Check((await penaltyService.GetChargeEntries(null)).Count == 0, "Posted bill is still eligible.");
    await Reject(async () => await penaltyService.PostCharges(1, request));
    await penaltyService.WaiveUnpaid(2, new() { "OVERDUE" });
    Check((await penaltyService.GetChargeEntries(null)).Count == 0, "Waived penalty would be charged again.");
});
await Test("Posting rechecks payment after loading", async () => {
    var bill = await OverdueBill();
    Check((await penaltyService.GetChargeEntries(null)).Count == 1, "Missing eligible bill.");
    bill.PaymentStatus = (int)PaymentStatus.Paid;
    await db.SaveChangesAsync();
    await Reject(async () => await penaltyService.PostCharges(1,
        new() { BillingReferenceIds = new() { "OVERDUE" } }));
    Check(await db.Penalties.CountAsync() == 0, "Paid bill received a penalty.");
});
await Test("Failed penalty posting rolls back the new charges", async () => {
    await OverdueBill();
    failure.Enabled = true;
    await Reject(async () => await penaltyService.PostCharges(1,
        new() { BillingReferenceIds = new() { "OVERDUE" } }));
    failure.Enabled = false;
    db.ChangeTracker.Clear();
    Check(await db.Penalties.CountAsync() == 0, "Failed posting persisted a penalty.");
});
await Test("Collection cannot settle a bill using a stale pre-penalty amount", async () => {
    await OverdueBill();
    await penaltyService.PostCharges(1, new() { BillingReferenceIds = new() { "OVERDUE" } });
    var paymentService = new PaymentService(db);
    var request = new TMCWD.Model.Billing.Requests.CollectionPaymentRequest {
        InvoiceNumber = "TEST-INVOICE", Method = PaymentMethod.Cash, AmountTendered = 1100,
        Bills = new() { new() { BillingReferenceId = "OVERDUE", PaidAmount = 1000 } } };
    await Reject(async () => await paymentService.ProcessCollectionPayment(1, request));
    db.ChangeTracker.Clear();
    Check(await db.Payments.CountAsync() == 0, "Stale amount was collected.");
    request.Bills[0].PaidAmount = 1100;
    await paymentService.ProcessCollectionPayment(1, request);
    Check((await db.Billings.SingleAsync()).PaymentStatus == (int)PaymentStatus.Paid, "Correct payment did not settle bill.");
});
await Test("Draft other charges enter Collection only after posting", async () => {
    await readingService.SaveUpdate(1, NewReading(0));
    Check(await sheetService.PartialPost(1, 1) == 1, "Bill was not posted.");
    var bill = await db.Billings.SingleAsync();
    var charge = await otherChargeService.SaveUpdate(1, new OtherCharge
    {
        BillingReferenceId = bill.BillingReferenceId,
        Type = 1,
        Amount = 50,
        PayableIn = "1",
        PaymentStatus = (int)PaymentStatus.Draft,
        IsActive = true
    });
    db.WaterConnectionApplications.Add(new WaterConnectionApplication
    {
        TrackingNumber = "APP-TEST-0001", FullName = "Test Applicant",
        Address = "Test Address", Barangay = "Test Barangay",
        ContactNumber = "09000000000", Email = "test@example.invalid",
        ConnectionType = "residential", Source = "walk_in", Stage = "charging",
        AccountId = 1, CustomerId = 1, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now
    });
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
    bill = await db.Billings.SingleAsync();
    Check(bill.RemainingAmount == 120 && bill.OtherCharges == 0,
        "A draft charge reached Collection before it was posted.");

    Check(await otherChargeService.PostToCollection(1, new() { (int)charge.Id }) == 1,
        "The draft charge was not posted.");
    db.ChangeTracker.Clear();
    bill = await db.Billings.SingleAsync();
    Check(bill.RemainingAmount == 120 && bill.OtherCharges == 0
        && (await new BillingService(db).GetCollectionBill(bill.BillingReferenceId))?.AmountDue == 170,
        "The posted installment was not separate from the water balance.");
    Check((await db.OtherCharges.SingleAsync()).PaymentStatus == (int)PaymentStatus.Unpaid,
        "The posted charge is not awaiting collection.");
    var application = await db.WaterConnectionApplications.SingleAsync();
    Check(application.Stage == "payment" && (application.ChargingData ?? "").Contains(bill.BillingReferenceId),
        "Posting the connection charge did not move the application to Payment.");
    Check(await otherChargeService.PostToCollection(1, new() { (int)charge.Id }) == 1,
        "Reposting an existing charge should safely reconcile the application.");
    db.ChangeTracker.Clear();
    Check((await db.Billings.SingleAsync()).RemainingAmount == 120 && await db.OtherChargeInstallments.CountAsync() == 1,
        "Reconciliation charged the posted amount twice.");

    var paymentService = new PaymentService(db);
    await paymentService.ProcessCollectionPayment(1,
        new TMCWD.Model.Billing.Requests.CollectionPaymentRequest
        {
            InvoiceNumber = "OTHER-CHARGE-INVOICE",
            Method = PaymentMethod.Cash,
            AmountTendered = 170,
            Bills = new() { new() { BillingReferenceId = bill.BillingReferenceId, PaidAmount = 170, InstallmentIds = new() { (await db.OtherChargeInstallments.SingleAsync()).Id } } }
        });
    db.ChangeTracker.Clear();
    Check((await db.OtherCharges.SingleAsync()).PaymentStatus == (int)PaymentStatus.Paid,
        "Collection did not settle the posted charge.");
    application = await db.WaterConnectionApplications.SingleAsync();
    Check(application.Stage == "installation" && (application.PaymentData ?? "").Contains((await db.Payments.SingleAsync()).PaymentReference),
        "Paying the connection charge did not move the application to Installation.");
});
await Test("Installation work order resolves its linked account number", async () => {
    var now = DateTime.Now;
    var application = new WaterConnectionApplication
    {
        TrackingNumber = "APP-INSTALL-ACCOUNT-0001", FullName = "Installation Applicant",
        Address = "Test Address", Barangay = "Test Barangay",
        ContactNumber = "09000000000", Email = "installation@example.invalid",
        ConnectionType = "residential", Source = "walk_in", Stage = "installation",
        AccountId = 1, CustomerId = 1, AccountNumber = null,
        CreatedAt = now, UpdatedAt = now
    };
    db.WaterConnectionApplications.Add(application);
    await db.SaveChangesAsync();

    var dashboard = new TMCWD.Data.Controllers.DashboardController(db);
    var response = await dashboard.WorkOrder("installation", application.Id);
    var ok = response.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
    var workOrder = ok?.Value as TMCWD.Model.Dashboard.ServiceWorkOrderRecord;

    db.ChangeTracker.Clear();
    var savedApplication = await db.WaterConnectionApplications.SingleAsync();
    Check(workOrder?.AccountNumber == "TEST",
        "Installation card did not resolve the linked account number.");
    Check(savedApplication.AccountNumber == "TEST",
        "The application account number was not backfilled for future card loads.");
});
await Test("Installation card creates a prelinked service request job order", async () => {
    var now = DateTime.Now;
    var application = new WaterConnectionApplication
    {
        TrackingNumber = "APP-INSTALL-SRO-0001", FullName = "Installation Applicant",
        Address = "Test Address", Barangay = "Test Barangay",
        ContactNumber = "09000000000", Email = "installation@example.invalid",
        ConnectionType = "residential", Source = "walk_in", Stage = "installation",
        AccountId = 1, CustomerId = 1, AccountNumber = "TEST",
        CreatedAt = now, UpdatedAt = now
    };
    db.WaterConnectionApplications.Add(application);
    await db.SaveChangesAsync();

    var dashboard = new TMCWD.Data.Controllers.DashboardController(db);
    var queueResponse = await dashboard.ServiceApplications("installation");
    var queueOk = queueResponse.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
    var queue = queueOk?.Value as List<TMCWD.Model.Dashboard.ServiceApplicationRecord>;
    Check(queue?.Single().AccountId == 1 && queue.Single().AccountNumber == "TEST"
        && queue.Single().Address.Contains("Test Barangay"),
        "Installation popup did not receive its linked account details.");

    var response = await dashboard.ServiceRequest(1, new TMCWD.Model.Dashboard.ServiceRequestOrderRecord
    {
        ApplicationId = application.Id,
        AccountId = 1,
        JobOrderDate = now.Date,
        Source = "Walk-in",
        JobOrderType = "Maintenance",
        JobDescription = "NEW CONNECTION INSTALLATION",
        ReportedBy = "Site Representative",
        ReferredToDepartment = "Maintenance",
        Particulars = "Install the new service connection."
    });
    var ok = response.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
    var created = ok?.Value as TMCWD.Model.Dashboard.ServiceRequestOrderRecord;

    db.ChangeTracker.Clear();
    var savedApplication = await db.WaterConnectionApplications.SingleAsync();
    var request = await db.Requests.SingleAsync();
    var detail = await db.RequestDetails.SingleAsync();
    var order = await db.JobOrders.SingleAsync();
    var createdJobNumber = created?.JobOrderNumber ?? string.Empty;
    Check(created?.AccountNumber == "TEST" && createdJobNumber.StartsWith("JO-"),
        "Installation popup did not create the linked service request.");
    Check(request.AccountId == 1 && request.CustomerId == 1 && request.IsServiceRequest
        && detail.RequestId == request.Id && order.RequestId == request.Id && order.RequestDetailId == detail.Id
        && detail.JobDescription == "NEW CONNECTION INSTALLATION"
        && request.ReportedBy == "Site Representative"
        && order.ReferredToUserId == -2 && created?.ReferredToName == "Maintenance",
        "Installation service request lost its account or job description.");
    Check(savedApplication.Stage == "connect"
        && createdJobNumber.Length > 0
        && (savedApplication.InstallationData ?? string.Empty).Contains(createdJobNumber),
        "Saved installation job order did not move the application to Connect.");
});
await Test("House Tapping saves meter details and completes the application", async () => {
    var now = DateTime.Now;
    var application = new WaterConnectionApplication
    {
        TrackingNumber = "APP-TAPPING-0001", FullName = "Tapping Applicant",
        Address = "Test Address", Barangay = "Test Barangay",
        ContactNumber = "09000000000", Email = "tapping@example.invalid",
        ConnectionType = "residential", Source = "walk_in", Stage = "tapping",
        AccountId = 1, CustomerId = 1, AccountNumber = "TEST",
        InspectionData = JsonSerializer.Serialize(new TMCWD.Model.Dashboard.ServiceInspectionRecord
        {
            InspectedByName = "Test Inspector", DateInspected = now.AddDays(-1)
        }),
        CreatedAt = now.AddDays(-2), UpdatedAt = now
    };
    db.WaterConnectionApplications.Add(application);
    await db.SaveChangesAsync();

    var dashboard = new TMCWD.Data.Controllers.DashboardController(db);
    var lookupResponse = await dashboard.HouseTapping(application.Id);
    var lookupOk = lookupResponse.Result as Microsoft.AspNetCore.Mvc.OkObjectResult;
    var tapping = lookupOk?.Value as TMCWD.Model.Dashboard.HouseTappingRecord;
    Check(tapping?.ApplicationNumber == "APP-TAPPING-0001"
        && tapping.InspectedByName == "Test Inspector"
        && tapping.ZoneBooks.Any(x => x.Zone == 1 && x.Book == 1)
        && tapping.MeterBrands.Contains("Test Meter Brand"),
        "House Tapping did not load its application, inspection, zone, or meter data.");

    var saveResponse = await dashboard.SaveHouseTapping(1, new TMCWD.Model.Dashboard.HouseTappingRecord
    {
        RequestId = application.Id, Zone = 1, Book = 1,
        MeterBrand = "Test Meter Brand", MeterSize = .75m,
        SerialNumber = "MTR-001", InitialReading = 10,
        AccomplishedByUserId = 1, DateAccomplished = now,
        Remarks = "House tapping completed."
    });
    Check(saveResponse.Result is Microsoft.AspNetCore.Mvc.OkObjectResult,
        "House Tapping save did not return success.");

    db.ChangeTracker.Clear();
    var savedApplication = await db.WaterConnectionApplications.SingleAsync();
    var savedAccount = await db.Accounts.SingleAsync();
    Check(savedApplication.Stage == "done"
        && (savedApplication.TappingData ?? string.Empty).Contains("Test Meter Brand")
        && savedApplication.DoneData == savedApplication.TappingData,
        "House Tapping did not complete and retain its application record.");
    Check(savedAccount.MeterNumber == "MTR-001" && savedAccount.MeterSize == .75m
        && savedAccount.Status == (int)AccountStatus.Active && savedAccount.ZoneBookId == 1,
        "House Tapping did not activate the account with its meter details.");

    var accountService = new AccountService(db);
    var hydratedAccount = await accountService.GetByAccountNumber("TEST");
    Check(hydratedAccount?.InitialReading == 10,
        "The tapping initial reading was not exposed as the account reading baseline.");

    await Reject(async () => await readingService.SaveUpdate(1, NewReading(9, 0)));
    var firstReading = await readingService.SaveUpdate(1, NewReading(15, 0));
    Check(firstReading.PreviousReading == 10 && firstReading.CurrentReading == 15
        && firstReading.CurrentReading - firstReading.PreviousReading == 5,
        "The first meter reading did not consume from the tapping baseline.");
});
await Test("Adjusted bill collects the remaining balance with one maintenance fee", async () => {
    await readingService.SaveUpdate(1, NewReading(0));
    Check(await sheetService.PartialPost(1, 1) == 1, "Bill was not posted.");
    var bill = await db.Billings.SingleAsync();
    Check(bill.TotalBillAmount == 120 && bill.RemainingAmount == 120,
        "The original bill must contain the â‚±100 minimum plus one â‚±20 maintenance fee.");

    var adjustmentService = new BillingAdjustmentService(db);
    await adjustmentService.SaveBatch(1, new()
    {
        new TMCWD.Data.Entities.BillingAdjustment
        {
            BillingReferenceId = bill.BillingReferenceId,
            Type = (int)AdjustmentLineType.CurrentBill,
            Amount = 30
        }
    }, Guid.NewGuid());
    db.ChangeTracker.Clear();
    bill = await db.Billings.SingleAsync();
    Check(bill.RemainingAmount == 90, "Adjustment did not update the collectible balance.");

    var paymentService = new PaymentService(db);
    await paymentService.ProcessCollectionPayment(1,
        new TMCWD.Model.Billing.Requests.CollectionPaymentRequest
        {
            InvoiceNumber = "ADJUSTED-INVOICE",
            Method = PaymentMethod.Cash,
            AmountTendered = 90,
            Bills = new() { new() { BillingReferenceId = bill.BillingReferenceId, PaidAmount = 90 } }
        });
    db.ChangeTracker.Clear();
    Check((await db.Payments.SingleAsync()).PaidAmount == 90,
        "The adjusted payment amount was not recorded exactly.");
    Check((await db.Billings.SingleAsync()).PaymentStatus == (int)PaymentStatus.Paid,
        "The adjusted bill was not settled.");
});
await Test("Senior Citizen discount applies 20 percent to water charge only", async () => {
    await readingService.SaveUpdate(1, NewReading(0));
    Check(await sheetService.PartialPost(1, 1) == 1, "Bill was not posted.");
    var bill = await db.Billings.SingleAsync();
    var paymentService = new PaymentService(db);
    await paymentService.ProcessCollectionPayment(1,
        new TMCWD.Model.Billing.Requests.CollectionPaymentRequest
        {
            InvoiceNumber = "SENIOR-INVOICE",
            Method = PaymentMethod.Cash,
            AmountTendered = 100,
            SeniorCitizenDiscount = new()
            {
                IdNumber = "SC-2026-0001",
                Name = "  tester,   senior citizen "
            },
            Bills = new() { new() { BillingReferenceId = bill.BillingReferenceId, PaidAmount = 100 } }
        });
    db.ChangeTracker.Clear();
    var payment = await db.Payments.SingleAsync();
    var adjustment = await db.BillingAdjustments.SingleAsync();
    var discount = await db.SeniorCitizenDiscounts.SingleAsync();
    Check(payment.PaidAmount == 100, "The discounted payment did not retain the maintenance fee.");
    Check(adjustment.Amount == 20 && adjustment.Type == (int)AdjustmentLineType.SeniorCitizenDiscount,
        "The 20 percent water-charge adjustment was not recorded.");
    Check(discount.WaterCharge == 100 && discount.DiscountAmount == 20 && discount.Rate == .20m,
        "The Senior Citizen discount audit record is incorrect.");
    Check(discount.SeniorCitizenIdNumber == "SC-2026-0001"
        && discount.SeniorCitizenName == "Tester, Senior Citizen",
        "The Senior Citizen identity was not recorded.");
});
await Test("Senior Citizen name must match the account holder", async () => {
    await readingService.SaveUpdate(1, NewReading(0));
    Check(await sheetService.PartialPost(1, 1) == 1, "Bill was not posted.");
    var bill = await db.Billings.SingleAsync();
    var paymentService = new PaymentService(db);
    await Reject(async () => await paymentService.ProcessCollectionPayment(1,
        new TMCWD.Model.Billing.Requests.CollectionPaymentRequest
        {
            InvoiceNumber = "WRONG-SENIOR",
            Method = PaymentMethod.Cash,
            AmountTendered = 100,
            SeniorCitizenDiscount = new() { IdNumber = "SC-2026-0002", Name = "Different Person" },
            Bills = new() { new() { BillingReferenceId = bill.BillingReferenceId, PaidAmount = 100 } }
        }));
    db.ChangeTracker.Clear();
    Check(await db.Payments.CountAsync() == 0 && await db.SeniorCitizenDiscounts.CountAsync() == 0,
        "A mismatched Senior Citizen discount was persisted.");
});
await Test("Cancelling a discounted invoice reopens the bill and preserves an audit trail", async () => {
    await readingService.SaveUpdate(1, NewReading(0));
    Check(await sheetService.PartialPost(1, 1) == 1, "Bill was not posted.");
    var bill = await db.Billings.SingleAsync();
    var paymentService = new PaymentService(db);
    await paymentService.ProcessCollectionPayment(1,
        new TMCWD.Model.Billing.Requests.CollectionPaymentRequest
        {
            InvoiceNumber = "CANCEL-ME",
            Method = PaymentMethod.Cash,
            AmountTendered = 100,
            SeniorCitizenDiscount = new()
            {
                IdNumber = "SC-2026-0003",
                Name = "Tester, Senior Citizen"
            },
            Bills = new() { new() { BillingReferenceId = bill.BillingReferenceId, PaidAmount = 100 } }
        });
    await paymentService.CancelInvoice(1,
        new TMCWD.Model.Billing.CancelInvoiceRequest
        {
            InvoiceNo = (await db.Payments.SingleAsync()).PaymentReference,
            Reason = "Wrong amount"
        });
    db.ChangeTracker.Clear();
    var reopened = await db.Billings.SingleAsync();
    Check(reopened.PaymentStatus == (int)PaymentStatus.Unpaid && reopened.RemainingAmount == 120,
        "Cancellation did not restore the original bill balance.");
    Check(!(await db.Payments.SingleAsync()).Success,
        "Cancelled payment remains successful.");
    Check((await db.SeniorCitizenDiscounts.SingleAsync()).IsCancelled,
        "Cancelled discount was not retained and marked cancelled.");
    Check(await db.InvoiceCancellations.AnyAsync(x => x.PaymentReference == db.Payments.Single().PaymentReference
            && x.Reason == "Wrong amount"),
        "Invoice cancellation audit record is missing.");
    Check(await db.BillingAdjustments.AnyAsync(x =>
            x.Type == (int)AdjustmentLineType.SeniorCitizenDiscountReversal && x.Amount == -20),
        "Discount reversal adjustment is missing.");
});
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
// Phase 6D regression tests
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

// Helper: creates a MobileReadingSheetController wired to the test DB.
MobileReadingSheetController MobileController(IBirInvoiceConfigService? bir = null, IPenaltyService? pen = null) =>
    new(sheetService, new UserService(db), new AccountService(db), readingService,
        new ZoneBookService(db), new CustomerService(db), new BillingService(db), bir, pen)
    {
        ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity(
                        [new System.Security.Claims.Claim(
                            System.Security.Claims.ClaimTypes.NameIdentifier, "10")], "test"))
            }
        }
    };

// Seed helpers shared across Phase 6D tests.
async Task SeedReaderAndAccount()
{
    if (!await db.Users.AnyAsync(u => u.Id == 10))
        db.Users.Add(new User { Id = 10, Name = "SI Reader", Email = "sireader@test.invalid",
            Password = "test", Role = (int)UserRole.MeterReader,
            IsActive = true, IsVerified = true, DateCreated = DateTime.Now, DateUpdated = DateTime.Now });
    var sheet = await db.ReadingSheets.SingleAsync();
    sheet.AssignedTo = 10;
    var account = await db.Accounts.SingleAsync(a => a.Id == 1);
    account.Status = (int)AccountStatus.Active;
    account.Sequence = 1;
    account.Street = "Synthetic invoice street";
    await db.SaveChangesAsync();
}

await Test("PartialPost assigns exactly one ServiceInvoiceNumber per billing", async () =>
{
    await SeedReaderAndAccount();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 8, ReadingDate = DateTime.Now });
    Check(await sheetService.PartialPost(1, 1) == 1, "PartialPost did not post the reading.");
    db.ChangeTracker.Clear();
    var billing = await db.Billings.SingleAsync();
    Check(!string.IsNullOrWhiteSpace(billing.ServiceInvoiceNumber),
        "PartialPost did not assign a ServiceInvoiceNumber.");
    Check(billing.ServiceInvoiceNumber == "TEST-000001", "Configured prefix/series/width were not used.");
});

await Test("Retry PartialPost does not generate another ServiceInvoiceNumber", async () =>
{
    await SeedReaderAndAccount();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 5, ReadingDate = DateTime.Now });
    Check(await sheetService.PartialPost(1, 1) == 1, "First post failed.");
    db.ChangeTracker.Clear();
    var first = (await db.Billings.SingleAsync()).ServiceInvoiceNumber;
    // Second call: reading already billed â€” recovered idempotently, no new bill created.
    // Force reading back to InProgress so PartialPost sees it and enters the recovery branch.
    var reading = await db.Readings.SingleAsync();
    reading.Status = ReadingStatus.InProgress;
    reading.IsCompleted = false;
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
    await sheetService.PartialPost(1, 1);
    db.ChangeTracker.Clear();
    Check(await db.Billings.CountAsync() == 1, "Retry created a duplicate billing row.");
    var second = (await db.Billings.SingleAsync()).ServiceInvoiceNumber;
    Check(first == second, "Retry changed the ServiceInvoiceNumber.");
});

await Test("ServiceInvoiceNumber is distinct from BillingReferenceId", async () =>
{
    await SeedReaderAndAccount();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 12, ReadingDate = DateTime.Now });
    await sheetService.PartialPost(1, 1);
    db.ChangeTracker.Clear();
    var billing = await db.Billings.SingleAsync();
    Check(!string.IsNullOrWhiteSpace(billing.BillingReferenceId), "BillingReferenceId missing.");
    Check(!string.IsNullOrWhiteSpace(billing.ServiceInvoiceNumber), "ServiceInvoiceNumber missing.");
    Check(billing.BillingReferenceId != billing.ServiceInvoiceNumber,
        "BillingReferenceId must remain an internal key distinct from ServiceInvoiceNumber.");
    // BillingReferenceId is still the 12-char hex key it was before Phase 6D.
    Check(billing.BillingReferenceId.Length == 12 &&
          billing.BillingReferenceId == billing.BillingReferenceId.ToUpperInvariant() &&
          System.Text.RegularExpressions.Regex.IsMatch(billing.BillingReferenceId, @"^[0-9A-F]{12}$"),
        "BillingReferenceId format was changed by Phase 6D.");
});

await Test("PaymentReference is null before payment; ServiceInvoiceNumber exists before payment", async () =>
{
    await SeedReaderAndAccount();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 7, ReadingDate = DateTime.Now });
    await sheetService.PartialPost(1, 1);
    db.ChangeTracker.Clear();
    var billing = await db.Billings.SingleAsync();
    Check(!string.IsNullOrWhiteSpace(billing.ServiceInvoiceNumber),
        "ServiceInvoiceNumber must exist before payment.");
    Check(string.IsNullOrWhiteSpace(billing.PaymentTransactionId),
        "PaymentTransactionId must be empty before payment.");
    Check(!await db.Payments.AnyAsync(), "Unexpected payment record before collection.");
});

await Test("SOA returns ServiceInvoiceNumber and QR payload without personal data", async () =>
{
    await SeedReaderAndAccount();
    await readingService.Sync(10, 1,
        new() { AccountId = 1, CurrentReading = 20, Remarks = "Normal Reading", ReadingDate = DateTime.Now });
    await sheetService.PartialPost(1, 1);
    db.ChangeTracker.Clear();
    var billing = await db.Billings.SingleAsync();
    var siNum = billing.ServiceInvoiceNumber!;
    var controller = MobileController();
    var result = ((await controller.Soa(1, 1)).Result as Microsoft.AspNetCore.Mvc.OkObjectResult)?.Value
        as TMCWD.Model.Billing.Responses.MobileSoaDto;
    Check(result != null, "SOA returned null.");
    Check(result!.ServiceInvoiceNumber == siNum,
        $"SOA ServiceInvoiceNumber mismatch: expected {siNum}, got {result.ServiceInvoiceNumber}.");
    Check(result.QrPayload == $"TMCWD|SI|{siNum}", "QR payload format incorrect.");
    // QR must not embed personal data â€” only the lookup key.
    Check(!result.QrPayload!.Contains(result.Name), "QR payload contains customer name.");
    Check(!result.QrPayload.Contains(result.Address), "QR payload contains address.");
    Check(!result.TotalBillAmount.ToString().All(c => result.QrPayload.Contains(c.ToString())),
        "QR payload appears to contain financial data.");
    Check(result.ReadingRemarks == "Normal Reading", "Reading remarks not surfaced in SOA.");
    Check(result.MeterReaderName == "SI Reader", "Meter reader name not surfaced in SOA.");
    Check(result.BirConfig == null, "BIR config must be null when no config has been set up.");
});

await Test("SOA includes active BIR config; missing config returned safely as null", async () =>
{
    await SeedReaderAndAccount();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 11, ReadingDate = DateTime.Now });
    await sheetService.PartialPost(1, 1);
    db.ChangeTracker.Clear();
    var birConfigService = new BirInvoiceConfigService(db);
    var activeConfig = new BirInvoiceConfig
    {
        PTUNumber = "PTU-TEST-001",
        PermitDateIssued = new DateOnly(2026, 1, 1),
        SeriesFrom = 1,
        SeriesTo = 999999,
        IsActive = true,
        CreatedBy = 1,
        DateCreated = DateTime.Now,
        UpdatedBy = 1,
        DateUpdated = DateTime.Now
    };
    await birConfigService.SaveUpdate(3, activeConfig);
    db.ChangeTracker.Clear();
    var controller = MobileController(birConfigService);
    var result = ((await controller.Soa(1, 1)).Result as Microsoft.AspNetCore.Mvc.OkObjectResult)?.Value
        as TMCWD.Model.Billing.Responses.MobileSoaDto;
    Check(result?.BirConfig != null, "Active BIR config must be present in SOA.");
    Check(result!.BirConfig!.PTUNumber == "PTU-TEST-001", "BIR PTU number mismatch.");
    Check(result.BirConfig.SeriesFrom == 1 && result.BirConfig.SeriesTo == 999999, "Series range mismatch.");
    // Deactivate to verify null path.
    activeConfig.IsActive = false;
    await birConfigService.SaveUpdate(3, activeConfig);
    db.ChangeTracker.Clear();
    var noConfig = ((await controller.Soa(1, 1)).Result as Microsoft.AspNetCore.Mvc.OkObjectResult)?.Value
        as TMCWD.Model.Billing.Responses.MobileSoaDto;
    Check(noConfig?.BirConfig == null, "Inactive BIR config must not appear in SOA.");
});

await Test("Activating a BIR config deactivates all others", async () =>
{
    var birConfigService = new BirInvoiceConfigService(db);
    var now = DateTime.Now;
    var a = await birConfigService.SaveUpdate(3, new BirInvoiceConfig
    {
        PTUNumber = "PTU-A", PermitDateIssued = new DateOnly(2025, 1, 1),
        SeriesFrom = 1, SeriesTo = 1000, IsActive = true,
        CreatedBy = 1, DateCreated = now, UpdatedBy = 1, DateUpdated = now
    });
    var b = await birConfigService.SaveUpdate(3, new BirInvoiceConfig
    {
        PTUNumber = "PTU-B", PermitDateIssued = new DateOnly(2026, 1, 1),
        SeriesFrom = 1001, SeriesTo = 2000, IsActive = true,
        CreatedBy = 1, DateCreated = now, UpdatedBy = 1, DateUpdated = now
    });
    db.ChangeTracker.Clear();
    Check((await db.BirInvoiceConfigs.CountAsync(c => c.IsActive)) == 1,
        "More than one BIR config is active after SaveUpdate.");
    var active = await birConfigService.GetActive();
    Check(active?.PTUNumber == "PTU-B", "Wrong config is active after second SaveUpdate.");
});

await Test("Collection lookup resolves ServiceInvoiceNumber to billing without processing payment", async () =>
{
    await SeedReaderAndAccount();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 9, ReadingDate = DateTime.Now });
    await sheetService.PartialPost(1, 1);
    db.ChangeTracker.Clear();
    var billing = await db.Billings.SingleAsync();
    var siNum = billing.ServiceInvoiceNumber!;
    Check(!string.IsNullOrWhiteSpace(siNum), "ServiceInvoiceNumber not assigned.");
    var lookupController = new ServiceInvoiceController(
        new BillingService(db), new AccountService(db), new CustomerService(db),
        new BirInvoiceConfigService(db));
    var ok = (await lookupController.GetByServiceInvoice(siNum)) as Microsoft.AspNetCore.Mvc.OkObjectResult;
    Check(ok != null, "Collection lookup returned non-OK for a valid service invoice.");
    var json = System.Text.Json.JsonSerializer.Serialize(ok!.Value,
        new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
    using var doc = System.Text.Json.JsonDocument.Parse(json);
    var root = doc.RootElement;
    Check(root.GetProperty("serviceInvoiceNumber").GetString() == siNum,
        "Lookup response serviceInvoiceNumber mismatch.");
    Check(root.GetProperty("billingReferenceId").GetString() == billing.BillingReferenceId,
        "Lookup response billingReferenceId mismatch.");
    Check(root.GetProperty("accountNumber").GetString() == "TEST",
        "Lookup response accountNumber mismatch.");
    // PaymentReference must NOT appear yet (payment hasn't happened).
    Check(root.GetProperty("paymentTransactionId").ValueKind == System.Text.Json.JsonValueKind.Null,
        "Lookup must not surface a payment reference before collection.");
    // Lookup must not change any database state.
    Check(await db.Payments.CountAsync() == 0, "Collection lookup created a payment record.");
    Check((await db.Billings.SingleAsync()).PaymentStatus == (int)PaymentStatus.Unpaid,
        "Collection lookup changed billing status.");
    // Lookup with an unknown invoice must return 404.
    var notFound = await lookupController.GetByServiceInvoice("TMCWD-999999-000000");
    Check(notFound is Microsoft.AspNetCore.Mvc.NotFoundObjectResult, "Unknown invoice must return 404.");
});

await Test("Meter maintenance is not double-counted in SOA or collection lookup", async () =>
{
    await SeedReaderAndAccount();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 0, ReadingDate = DateTime.Now });
    await sheetService.PartialPost(1, 1);
    db.ChangeTracker.Clear();
    var billing = await db.Billings.SingleAsync();
    // Minimum bill = MinimumCharge (100) + MMF (20) = 120.
    Check(billing.TotalBillAmount == 120m, "TotalBillAmount must include MMF exactly once.");
    var controller = MobileController();
    var soa = ((await controller.Soa(1, 1)).Result as Microsoft.AspNetCore.Mvc.OkObjectResult)?.Value
        as TMCWD.Model.Billing.Responses.MobileSoaDto;
    Check(soa != null, "SOA returned null for posted reading.");
    Check(soa!.TotalBillAmount == billing.TotalBillAmount,
        "SOA must return the persisted TotalBillAmount without recalculation.");
    Check(soa.TotalBillAmount == 120m,
        "SOA TotalBillAmount must be exactly 120 (100 minimum + 20 MMF), not double-counted.");
});

await Test("Penalty uses persisted Penalty rows; PCA and arrears are absent from SOA", async () =>
{
    await SeedReaderAndAccount();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 0, ReadingDate = DateTime.Now });
    await sheetService.PartialPost(1, 1);
    db.ChangeTracker.Clear();
    var billing = await db.Billings.SingleAsync();
    // Post a persisted penalty row.
    var sheet = await db.ReadingSheets.SingleAsync();
    sheet.DueDate = TMCWD.Model.Billing.LatePaymentPenalty.Today.AddDays(-1);
    await db.SaveChangesAsync();
    await penaltyService.PostCharges(1,
        new TMCWD.Model.Billing.PostPenaltyChargesRequest
        { BillingReferenceIds = new() { billing.BillingReferenceId } });
    db.ChangeTracker.Clear();
    var penalty = await db.Penalties.SingleAsync();
    Check(penalty.Amount == 10m, // 10% of 100 = 10
        $"Persisted penalty must be 10% of bill; expected 10, got {penalty.Amount}.");
    // SOA reads the persisted billing row â€” penalty is not embedded in TotalBillAmount.
    var controller = MobileController(pen: penaltyService);
    var soa = ((await controller.Soa(1, 1)).Result as Microsoft.AspNetCore.Mvc.OkObjectResult)?.Value
        as TMCWD.Model.Billing.Responses.MobileSoaDto;
    Check(soa != null, "SOA returned null after penalty post.");
    Check(soa!.TotalBillAmount == 120m, "Penalty must not be added to TotalBillAmount in SOA.");
    // CurrentPenaltyAmount: sum of persisted Penalty rows (10 = 10% of 100).
    // PenaltyAfterDueAmount: informational 10% of BillAmount (server-derived, same rounding).
    // TotalAfterDue: TotalDue + PenaltyAfterDueAmount.
    // PCA and Arrears remain NULL (no authoritative rule).
    Check(soa.PowerCostAdjustment == null && soa.ArrearsAmount == 0m,
        "No eligible prior unpaid bills must report zero arrears after penalty posting.");
    Check(soa.CurrentPenaltyAmount == 10m,
        $"CurrentPenaltyAmount must be the sum of persisted Penalty rows (10); got {soa.CurrentPenaltyAmount}.");
    Check(soa.TotalDue == 120m,
        $"TotalDue must come from the snapshot (120); got {soa.TotalDue}.");
    Check(soa.PenaltyAfterDueAmount == 10m,
        $"PenaltyAfterDueAmount must be 10% of BillAmount (10); got {soa.PenaltyAfterDueAmount}.");
    Check(soa.TotalAfterDue == 130m,
        $"TotalAfterDue must be TotalDue + PenaltyAfterDue (130); got {soa.TotalAfterDue}.");
});

await Test("Sequential ServiceInvoiceNumbers are unique across multiple postings", async () =>
{
    // Add a second account to the roster so PartialPost generates two bills.
    db.Accounts.Add(new Account { Id = 2, CustomerId = 1, AccountNumber = "TEST-02",
        ZoneBookId = 1, Sequence = 2, Status = (int)AccountStatus.Active,
        Classification = 1, MeterSize = .5m, DateCreated = DateTime.Now, DateUpdated = DateTime.Now });
    await SeedReaderAndAccount();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 5, ReadingDate = DateTime.Now });
    await readingService.Sync(10, 1, new() { AccountId = 2, CurrentReading = 8, ReadingDate = DateTime.Now });
    Check(await sheetService.PartialPost(1, 1) == 2, "Two readings should be posted.");
    db.ChangeTracker.Clear();
    var invoiceNumbers = (await db.Billings.AsNoTracking().ToListAsync())
        .Select(b => b.ServiceInvoiceNumber).ToList();
    Check(invoiceNumbers.All(n => !string.IsNullOrWhiteSpace(n)),
        "Every billing must receive a ServiceInvoiceNumber.");
    Check(invoiceNumbers.Distinct().Count() == 2,
        "ServiceInvoiceNumbers must be unique across concurrent postings.");
});

await Test("BIR range starts at SeriesFrom, preserves zeros, ends safely and switches per config", async () => {
    await SeedReaderAndAccount();
    var config = await db.BirInvoiceConfigs.SingleAsync();
    config.SeriesFrom = 8; config.SeriesTo = 9; config.SeriesWidth = 7; config.InvoicePrefix = "CUSTOM-";
    await db.SaveChangesAsync();
    var billingService = new BillingService(db);
    async Task<Billing> Assign(string reference) {
        await using var tx = await db.Database.BeginTransactionAsync();
        var bill = new Billing { AccountId = 1, BillingReferenceId = reference, BillingPeriod = DateTime.Now,
            DateCreated = DateTime.Now, DateUpdated = DateTime.Now, PaymentStatus = (int)PaymentStatus.Unpaid };
        await billingService.AssignServiceInvoiceNumber(bill); db.Billings.Add(bill);
        await db.SaveChangesAsync(); await tx.CommitAsync(); return bill;
    }
    var first = await Assign("FIRST-RANGE");
    Check(first.ServiceInvoiceNumber == "CUSTOM-0000008", "First serial/leading zero/prefix mismatch.");
    var last = await Assign("LAST-RANGE");
    Check(last.ServiceInvoiceNumber == "CUSTOM-0000009", "Last serial did not succeed.");
    var reading = await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 5, ReadingDate = DateTime.Now });
    await Reject(async () => await sheetService.PartialPost(1, 1));
    db.ChangeTracker.Clear();
    Check(await db.Billings.CountAsync() == 2 && (await db.ServiceInvoiceSeq.SingleAsync()).LastValue == 9
        && (await db.Readings.SingleAsync()).Status == ReadingStatus.InProgress, "Exhaustion consumed a serial or partially posted work.");
    var newConfig = await new BirInvoiceConfigService(db).SaveUpdate(3, new BirInvoiceConfig {
        PTUNumber = "NEW-SERIES", PermitDateIssued = new DateOnly(2026, 1, 1), SeriesFrom = 30, SeriesTo = 31, SeriesWidth = 4, IsActive = true });
    Check(await sheetService.PartialPost(1, 1) == 1, "New series could not post.");
    db.ChangeTracker.Clear();
    var posted = await db.Billings.SingleAsync(b => b.ReadingId == reading.Id);
    Check(posted.ServiceInvoiceNumber == "0030", "New config did not start at its own range or invented a prefix.");
    Check(await sheetService.PartialPost(1, 1) == 0 && (await db.ServiceInvoiceSeq.SingleAsync(s => s.Id == newConfig.Id)).LastValue == 30,
        "Retry consumed a serial.");
    posted.ServiceInvoiceNumber = "FORGED";
    await Reject(async () => await billingService.SaveUpdate(3, posted));
    db.ChangeTracker.Clear();
    Check((await db.Billings.SingleAsync(b => b.Id == posted.Id)).ServiceInvoiceNumber == "0030", "Assigned invoice changed.");
});
await Test("BIR config validates ranges, requires SuperAdmin and preserves used series", async () => {
    await SeedReaderAndAccount();
    var service = new BirInvoiceConfigService(db);
    async Task Invalid(BirInvoiceConfig c) {
        try { await service.SaveUpdate(3, c); } catch (ArgumentException) { return; }
        throw new Exception("Invalid BIR config accepted.");
    }
    await Invalid(new() { PTUNumber = "INVALID-RANGE", SeriesFrom = 20, SeriesTo = 10 });
    await Invalid(new() { PTUNumber = "INVALID-WIDTH", SeriesFrom = 1, SeriesTo = 999, SeriesWidth = 2 });
    await RejectUnauthorized(async () => await service.SaveUpdate(10, new() { SeriesFrom = 1, SeriesTo = 10 }));
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 5, ReadingDate = DateTime.Now });
    await sheetService.PartialPost(1, 1);
    db.ChangeTracker.Clear();
    var config = await db.BirInvoiceConfigs.SingleAsync(); config.SeriesTo--;
    await Reject(async () => await service.SaveUpdate(3, config));
    db.ChangeTracker.Clear();
    Check((await db.BirInvoiceConfigs.SingleAsync()).SeriesTo == 999999, "Used series was edited.");
});
await Test("Missing active BIR config rejects posting without billing/status/sequence mutations", async () => {
    await SeedReaderAndAccount();
    (await db.BirInvoiceConfigs.SingleAsync()).IsActive = false; await db.SaveChangesAsync();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 5, ReadingDate = DateTime.Now });
    await Reject(async () => await sheetService.PartialPost(1, 1));
    db.ChangeTracker.Clear();
    Check(!await db.Billings.AnyAsync() && !await db.ServiceInvoiceSeq.AnyAsync()
        && (await db.Readings.SingleAsync()).Status == ReadingStatus.InProgress, "Missing config caused partial posting.");
});
await Test("Collection payment keeps the invoice number, internal billing key and payment reference separate", async () => {
    await SeedReaderAndAccount();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 5, ReadingDate = DateTime.Now });
    await sheetService.PartialPost(1, 1);
    var bill = await db.Billings.SingleAsync();
    var invoice = bill.ServiceInvoiceNumber;
    var reference = bill.BillingReferenceId;
    await new PaymentService(db).ProcessCollectionPayment(1, new TMCWD.Model.Billing.Requests.CollectionPaymentRequest {
        InvoiceNumber = "OR-SYNTHETIC-001", Method = PaymentMethod.Cash, AmountTendered = bill.RemainingAmount,
        Bills = new() { new() { BillingReferenceId = reference, PaidAmount = bill.RemainingAmount } }
    });
    db.ChangeTracker.Clear();
    var paid = await db.Billings.SingleAsync(); var payment = await db.Payments.SingleAsync();
    Check(paid.ServiceInvoiceNumber == invoice && paid.BillingReferenceId == reference
        && payment.BillingReference == reference && payment.PaymentReference == (await db.PaymentReceipts.SingleAsync()).PaymentReference
        && paid.PaymentTransactionId == payment.PaymentReference && payment.PaymentReference != invoice,
        "Collection replaced or conflated invoice, billing or payment identifiers.");
});
await Test("Historical NULL service invoice remains NULL with no fabricated QR", async () => {
    await SeedReaderAndAccount();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 5, ReadingDate = DateTime.Now });
    var reading = await db.Readings.SingleAsync(); reading.Status = ReadingStatus.Completed; reading.IsCompleted = true;
    db.Billings.Add(new Billing { AccountId = 1, ReadingId = reading.Id, BillingReferenceId = "HISTORICAL",
        BillingPeriod = DateTime.Now, TotalBillAmount = 123m, RemainingAmount = 123m,
        DateCreated = DateTime.Now, DateUpdated = DateTime.Now, PaymentStatus = (int)PaymentStatus.Unpaid });
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    var soa = ((await MobileController().Soa(1, 1)).Result as Microsoft.AspNetCore.Mvc.OkObjectResult)?.Value
        as TMCWD.Model.Billing.Responses.MobileSoaDto;
    Check(soa != null && soa.ServiceInvoiceNumber == null && soa.QrPayload == null && soa.BillingReferenceId == "HISTORICAL",
        "Historical invoice numbers or QR were fabricated.");
});
foreach (var (previousDate, currentDate) in new[] {
    (new DateTime(2026, 9, 5), new DateTime(2026, 10, 5)),
    (new DateTime(2026, 9, 30), new DateTime(2026, 10, 31)),
    (new DateTime(2026, 12, 5), new DateTime(2027, 1, 5)) })
await Test($"Consumption period freezes authoritative dates {previousDate:yyyy-MM-dd} to {currentDate:yyyy-MM-dd}", async () => {
    await SeedReaderAndAccount();
    var sheet = await db.ReadingSheets.SingleAsync();
    sheet.BillingDate = currentDate.AddDays(1);
    sheet.DueDate = sheet.BillingDate.Value.AddDays(15);
    sheet.BillingPeriodStart = currentDate; // Scheduling date must not become PeriodFrom.
    db.ReadingSheets.Add(new ReadingSheet { Id = 2, Status = ReadingStatus.Completed, BillingDate = previousDate.AddDays(1) });
    db.Readings.AddRange(
        new Reading { AccountId = 1, ReadingSheetId = 2, CurrentReading = 5, Status = ReadingStatus.Completed, DateCreated = previousDate },
        new Reading { AccountId = 1, ReadingSheetId = 2, CurrentReading = 2, Status = ReadingStatus.Completed, DateCreated = previousDate.AddDays(-10) },
        new Reading { AccountId = 999, ReadingSheetId = 2, CurrentReading = 99, Status = ReadingStatus.Completed, DateCreated = currentDate.AddDays(-1) },
        new Reading { AccountId = 1, ReadingSheetId = 2, CurrentReading = 99, Status = ReadingStatus.Deleted, DateCreated = currentDate.AddDays(-1) },
        new Reading { AccountId = 1, ReadingSheetId = 2, CurrentReading = 5, Status = ReadingStatus.Completed, DateCreated = currentDate.AddDays(1) });
    await db.SaveChangesAsync();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 10, ReadingDate = currentDate });
    await sheetService.PartialPost(1, 1);
    db.ChangeTracker.Clear();
    var bill = await db.Billings.SingleAsync();
    var frozen = bill.InvoiceSnapshotJson;
    var snapshot = JsonSerializer.Deserialize<TMCWD.Model.Billing.OfficialInvoiceSnapshot>(frozen!)!;
    Check(snapshot.PeriodFrom == previousDate && snapshot.PeriodTo == currentDate,
        "Consumption period selected another account, scheduling date, deleted, current or future reading.");
    var previous = await db.Readings.SingleAsync(r => r.AccountId == 1 && r.DateCreated == previousDate);
    previous.DateCreated = previousDate.AddDays(-2);
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
    for (var i = 0; i < 2; i++) {
        var soa = ((await MobileController().Soa(1, 1)).Result as Microsoft.AspNetCore.Mvc.OkObjectResult)?.Value
            as TMCWD.Model.Billing.Responses.MobileSoaDto;
        Check(soa?.PeriodFrom == previousDate && soa.PeriodTo == currentDate && soa.ReadingDate == currentDate
            && soa.BillingDate == currentDate.AddDays(1) && soa.DueDate == currentDate.AddDays(16),
            "SOA recomputed the frozen consumption period or changed official dates.");
    }
    Check(await sheetService.PartialPost(1, 1) == 0
        && (await db.Billings.AsNoTracking().SingleAsync()).InvoiceSnapshotJson == frozen,
        "Repeated posting or SOA retrieval mutated the invoice snapshot.");
});
foreach (var water in new[] { 170m, 900m })
await Test($"Penalty uses water only ({water}) and excludes maintenance and later charges", async () => {
    await SeedReaderAndAccount();
    var rate = await db.WaterRates.SingleAsync();
    rate.MinimumCharge = water;
    await db.SaveChangesAsync();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 0, ReadingDate = DateTime.Now });
    await sheetService.PartialPost(1, 1);
    db.ChangeTracker.Clear();
    var bill = await db.Billings.SingleAsync();
    var frozen = bill.InvoiceSnapshotJson;
    bill.OtherCharges = 77m; bill.BillingAdjustment = 33m;
    bill.TotalBillAmount += 110m; bill.RemainingAmount += 110m;
    var sheet = await db.ReadingSheets.SingleAsync();
    var today = TMCWD.Model.Billing.LatePaymentPenalty.Today;
    foreach (var due in new[] { today.AddDays(1), today }) {
        sheet.DueDate = due;
        await db.SaveChangesAsync();
        Check((await penaltyService.GetChargeEntries(null, today.AddDays(30))).Count == 0,
            "Explicit cutoff must not charge before or on the due date.");
        await Reject(async () => await penaltyService.PostCharges(1, new() {
            DueDate = today.AddDays(30), BillingReferenceIds = new() { bill.BillingReferenceId } }));
    }
    sheet.DueDate = today.AddDays(-1);
    await db.SaveChangesAsync();
    var eligible = (await penaltyService.GetChargeEntries(null)).Single();
    Check(eligible.BillAmount == water && eligible.Penalty == water / 10m,
        "Penalty preview includes non-water components.");
    await penaltyService.PostCharges(1, new() { BillingReferenceIds = new() { bill.BillingReferenceId } });
    db.ChangeTracker.Clear();
    Check((await db.Penalties.SingleAsync()).Amount == water / 10m, "Posted penalty includes non-water components.");
    for (var i = 0; i < 2; i++) {
        var soa = ((await MobileController(pen: penaltyService).Soa(1, 1)).Result as Microsoft.AspNetCore.Mvc.OkObjectResult)?.Value
            as TMCWD.Model.Billing.Responses.MobileSoaDto;
        Check(soa?.PenaltyAfterDueAmount == water / 10m && soa.TotalAfterDue == water + 20m + water / 10m
            && soa.CurrentPenaltyAmount == water / 10m, "SOA penalty or after-due total uses the wrong base.");
    }
    Check((await db.Billings.AsNoTracking().SingleAsync()).InvoiceSnapshotJson == frozen
        && await db.Penalties.CountAsync() == 1 && await db.Billings.CountAsync() == 1,
        "SOA GET mutated the snapshot or created financial records.");
});
await Test("Historical missing water snapshot rejects penalty without changing records", async () => {
    var bill = await OverdueBill();
    bill.InvoiceSnapshotJson = null;
    await db.SaveChangesAsync();
    await Reject(async () => await penaltyService.PostCharges(1, new() { BillingReferenceIds = new() { bill.BillingReferenceId } }));
    Check(await db.Penalties.CountAsync() == 0 && (await db.Billings.SingleAsync()).InvoiceSnapshotJson == null,
        "Missing historical water charge fabricated a penalty or snapshot.");
});
await Test("Official invoice snapshots preserve tier lines, maintenance, periods and identity after rate/payment changes", async () => {
    await SeedReaderAndAccount();
    var date = DateTime.Now;
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 45, ReadingDate = date });
    await sheetService.PartialPost(1, 1);
    db.ChangeTracker.Clear();
    var bill = await db.Billings.SingleAsync();
    var before = bill.InvoiceSnapshotJson;
    var number = bill.ServiceInvoiceNumber;
    var snapshot = JsonSerializer.Deserialize<TMCWD.Model.Billing.OfficialInvoiceSnapshot>(before!)!;
    Check(snapshot.BillAmount == 900m && snapshot.MeterMaintenance == 20m && bill.TotalBillAmount == 920m,
        "Invoice water/maintenance must sum to the existing posted bill without double-counting.");
    Check(snapshot.ChargeLines.Count == 5 && snapshot.ChargeLines.Sum(x => x.Amount) == snapshot.BillAmount
        && snapshot.ChargeLines.Last().Quantity == 5m && snapshot.ChargeLines.Last().UnitCost == 40m,
        "Tier snapshot quantities, costs or amounts changed.");
    Check(snapshot.SchemaVersion == 1,
        "Snapshot must carry schemaVersion 1.");
    Check(snapshot.TotalDue == 920m,
        $"Snapshot TotalDue must be BillAmount + MeterMaintenance (920); got {snapshot.TotalDue}.");
    Check(snapshot.PeriodFrom == null && snapshot.PeriodTo?.Date == date.Date && snapshot.ArrearsAmount == 0m
        && snapshot.PowerCostAdjustment == null,
        "Period end must be the stored reading date; no prior arrears must be zero, unsupported rules remain NULL.");
    var rate = await db.WaterRates.SingleAsync(); rate.MinimumCharge = 999m; rate.Rate41Up = 500m;
    var bir = await db.BirInvoiceConfigs.SingleAsync(); bir.PTUNumber = "SYNTHETIC-UPDATED-PTU";
    var postedReading = await db.Readings.SingleAsync(); postedReading.DateCreated = date.AddDays(2);
    bill.RemainingAmount = 0m; bill.PaymentStatus = (int)PaymentStatus.Paid;
    await db.SaveChangesAsync();
    var first = ((await MobileController().Soa(1, 1)).Result as Microsoft.AspNetCore.Mvc.OkObjectResult)!.Value as TMCWD.Model.Billing.Responses.MobileSoaDto;
    var second = ((await MobileController().Soa(1, 1)).Result as Microsoft.AspNetCore.Mvc.OkObjectResult)!.Value as TMCWD.Model.Billing.Responses.MobileSoaDto;
    Check(first!.BillAmount == 900m && first.MeterMaintenance == 20m && first.ChargeLines!.SequenceEqual(snapshot.ChargeLines)
        && first.PeriodFrom == snapshot.PeriodFrom && first.PeriodTo == snapshot.PeriodTo
        && JsonSerializer.Serialize(first) == JsonSerializer.Serialize(second)
        && first.ServiceInvoiceNumber == number && first.QrPayload == "TMCWD|SI|" + number,
        "SOA must read the exact issued snapshot and stable identity, never recalculate it.");
    Check(await sheetService.PartialPost(1, 1) == 0 && (await db.Billings.AsNoTracking().SingleAsync()).InvoiceSnapshotJson == before,
        "Repeated GET or posting changed the persisted invoice snapshot.");
});
await Test("Day before DueDate: CurrentPenaltyAmount is null (no rows), PenaltyAfterDue and TotalAfterDue are informational", async () =>
{
    await SeedReaderAndAccount();
    var sheet = await db.ReadingSheets.SingleAsync();
    sheet.DueDate = TMCWD.Model.Billing.LatePaymentPenalty.Today.AddDays(1); // tomorrow = not yet overdue
    await db.SaveChangesAsync();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 0, ReadingDate = DateTime.Now });
    await sheetService.PartialPost(1, 1);
    db.ChangeTracker.Clear();
    var billing = await db.Billings.SingleAsync();
    // TotalDue should be in the snapshot.
    var snapshot = System.Text.Json.JsonSerializer.Deserialize<TMCWD.Model.Billing.OfficialInvoiceSnapshot>(billing.InvoiceSnapshotJson!)!;
    Check(snapshot.TotalDue == 120m, "Snapshot TotalDue must be 120 (100 min + 20 MMF).");
    var controller = new TMCWD.Data.Controllers.MobileReadingSheetController(
        sheetService, new UserService(db), new AccountService(db), readingService,
        new ZoneBookService(db), new CustomerService(db), new BillingService(db), null, penaltyService)
    {
        ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity(
                        [new System.Security.Claims.Claim(
                            System.Security.Claims.ClaimTypes.NameIdentifier, "10")], "test"))
            }
        }
    };
    var soa = ((await controller.Soa(1, 1)).Result as Microsoft.AspNetCore.Mvc.OkObjectResult)?.Value
        as TMCWD.Model.Billing.Responses.MobileSoaDto;
    Check(soa != null, "SOA null on day before DueDate.");
    // No Penalty rows yet â€” CurrentPenaltyAmount must be null, not zero.
    Check(soa!.CurrentPenaltyAmount == null,
        $"CurrentPenaltyAmount must be null before DueDate (no rows); got {soa.CurrentPenaltyAmount}.");
    // PenaltyAfterDue is informational and must be available before DueDate.
    Check(soa.PenaltyAfterDueAmount == 10m,
        $"PenaltyAfterDueAmount must be 10% of BillAmount (10) before DueDate; got {soa.PenaltyAfterDueAmount}.");
    Check(soa.TotalAfterDue == 130m,
        $"TotalAfterDue must be 130 before DueDate; got {soa.TotalAfterDue}.");
    // SOA GET must not create any Penalty record.
    Check(!await db.Penalties.AnyAsync(), "SOA GET created a penalty record.");
});

await Test("Exact DueDate: CurrentPenaltyAmount null, informational PenaltyAfterDue still correct", async () =>
{
    await SeedReaderAndAccount();
    var sheet = await db.ReadingSheets.SingleAsync();
    sheet.DueDate = TMCWD.Model.Billing.LatePaymentPenalty.Today; // today = on DueDate
    await db.SaveChangesAsync();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 0, ReadingDate = DateTime.Now });
    await sheetService.PartialPost(1, 1);
    db.ChangeTracker.Clear();
    var controller = new TMCWD.Data.Controllers.MobileReadingSheetController(
        sheetService, new UserService(db), new AccountService(db), readingService,
        new ZoneBookService(db), new CustomerService(db), new BillingService(db), null, penaltyService)
    {
        ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity(
                        [new System.Security.Claims.Claim(
                            System.Security.Claims.ClaimTypes.NameIdentifier, "10")], "test"))
            }
        }
    };
    var soa = ((await controller.Soa(1, 1)).Result as Microsoft.AspNetCore.Mvc.OkObjectResult)?.Value
        as TMCWD.Model.Billing.Responses.MobileSoaDto;
    Check(soa != null, "SOA null on exact DueDate.");
    Check(soa!.CurrentPenaltyAmount == null, "CurrentPenaltyAmount must be null on DueDate (no rows).");
    Check(soa.PenaltyAfterDueAmount == 10m, "PenaltyAfterDue must be informational 10 on DueDate.");
    Check(soa.TotalAfterDue == 130m, "TotalAfterDue must be 130 on DueDate.");
    Check(!await db.Penalties.AnyAsync(), "SOA created a penalty on DueDate.");
});

await Test("Day after DueDate: CurrentPenaltyAmount matches persisted penalty row", async () =>
{
    await SeedReaderAndAccount();
    var sheet = await db.ReadingSheets.SingleAsync();
    sheet.DueDate = TMCWD.Model.Billing.LatePaymentPenalty.Today.AddDays(-1); // yesterday = overdue
    await db.SaveChangesAsync();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 0, ReadingDate = DateTime.Now });
    await sheetService.PartialPost(1, 1);
    db.ChangeTracker.Clear();
    var billing = await db.Billings.SingleAsync();
    await penaltyService.PostCharges(1, new() { BillingReferenceIds = new() { billing.BillingReferenceId } });
    db.ChangeTracker.Clear();
    var controller = new TMCWD.Data.Controllers.MobileReadingSheetController(
        sheetService, new UserService(db), new AccountService(db), readingService,
        new ZoneBookService(db), new CustomerService(db), new BillingService(db), null, penaltyService)
    {
        ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity(
                        [new System.Security.Claims.Claim(
                            System.Security.Claims.ClaimTypes.NameIdentifier, "10")], "test"))
            }
        }
    };
    var soa = ((await controller.Soa(1, 1)).Result as Microsoft.AspNetCore.Mvc.OkObjectResult)?.Value
        as TMCWD.Model.Billing.Responses.MobileSoaDto;
    Check(soa != null, "SOA null after overdue penalty posted.");
    Check(soa!.CurrentPenaltyAmount == 10m,
        $"CurrentPenaltyAmount must match the persisted penalty row (10); got {soa.CurrentPenaltyAmount}.");
    Check(soa.PenaltyAfterDueAmount == 10m, "PenaltyAfterDue must still be 10 after posting.");
    Check(soa.TotalAfterDue == 130m, "TotalAfterDue must remain 130 after posting.");
});

await Test("SOA GET never creates or mutates Penalty records", async () =>
{
    await SeedReaderAndAccount();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 0, ReadingDate = DateTime.Now });
    await sheetService.PartialPost(1, 1);
    db.ChangeTracker.Clear();
    var controller = new TMCWD.Data.Controllers.MobileReadingSheetController(
        sheetService, new UserService(db), new AccountService(db), readingService,
        new ZoneBookService(db), new CustomerService(db), new BillingService(db), null, penaltyService)
    {
        ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity(
                        [new System.Security.Claims.Claim(
                            System.Security.Claims.ClaimTypes.NameIdentifier, "10")], "test"))
            }
        }
    };
    // Call SOA many times.
    for (var i = 0; i < 5; i++)
        await controller.Soa(1, 1);
    db.ChangeTracker.Clear();
    Check(!await db.Penalties.AnyAsync(), "Repeated SOA GET created Penalty records.");
    Check(await db.Billings.CountAsync() == 1, "SOA GET changed billing count.");
});

await Test("Snapshot schemaVersion is 1 and TotalDue equals water+maintenance at post time", async () =>
{
    await SeedReaderAndAccount();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 15, ReadingDate = DateTime.Now });
    await sheetService.PartialPost(1, 1);
    db.ChangeTracker.Clear();
    var billing = await db.Billings.SingleAsync();
    Check(billing.InvoiceSnapshotJson != null, "Snapshot JSON is missing.");
    var snapshot = System.Text.Json.JsonSerializer.Deserialize<TMCWD.Model.Billing.OfficialInvoiceSnapshot>(billing.InvoiceSnapshotJson!)!;
    Check(snapshot.SchemaVersion == 1, $"SchemaVersion must be 1; got {snapshot.SchemaVersion}.");
    var expectedWater = snapshot.ChargeLines.Sum(l => l.Amount);
    Check(snapshot.TotalDue == expectedWater + snapshot.MeterMaintenance,
        $"TotalDue must equal water ({expectedWater}) + maintenance ({snapshot.MeterMaintenance}); got {snapshot.TotalDue}.");
    Check(snapshot.TotalDue == billing.TotalBillAmount,
        "Snapshot TotalDue must match the posted TotalBillAmount at post time.");
});

await Test("Meter Maintenance is counted exactly once in TotalDue and TotalBillAmount", async () =>
{
    await SeedReaderAndAccount();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 0, ReadingDate = DateTime.Now });
    await sheetService.PartialPost(1, 1);
    db.ChangeTracker.Clear();
    var billing = await db.Billings.SingleAsync();
    var snapshot = System.Text.Json.JsonSerializer.Deserialize<TMCWD.Model.Billing.OfficialInvoiceSnapshot>(billing.InvoiceSnapshotJson!)!;
    // The minimum bill: water = 100 (min charge), maintenance = 20, TotalDue = 120.
    Check(snapshot.BillAmount == 100m, $"Water BillAmount must be 100; got {snapshot.BillAmount}.");
    Check(snapshot.MeterMaintenance == 20m, $"MeterMaintenance must be 20; got {snapshot.MeterMaintenance}.");
    Check(snapshot.TotalDue == 120m, $"TotalDue must be 120; got {snapshot.TotalDue}.");
    // The persisted billing total must match.
    Check(billing.TotalBillAmount == 120m, $"TotalBillAmount must be 120; got {billing.TotalBillAmount}.");
    // The aggregate OtherCharges in the snapshot is zero at post time; Meter Maintenance
    // is NOT an OtherCharge â€” it is already part of the water bill.
    Check(snapshot.OtherCharges == 0m,
        "OtherCharges in the snapshot must be zero at post time (no additional charges posted yet).");
    // TotalDue must NOT be BillAmount + MeterMaintenance + OtherCharges with MeterMaintenance in both.
    Check(snapshot.TotalDue == snapshot.BillAmount + snapshot.MeterMaintenance,
        "TotalDue must be BillAmount + MeterMaintenance only (Meter Maintenance must not be double-counted).");
});

foreach (var (previous, current, valid) in new[] { (0m,0m,true), (0m,1m,true), (5m,5m,true), (5m,4m,false) })
    await Test($"Confirmed sync rule: {previous} -> {current}, valid={valid}", async () => {
        await SeedReaderAndAccount();
        if (previous > 0) { db.Readings.Add(NewReading(previous, previous)); await db.SaveChangesAsync(); }
        try {
            var saved = await readingService.Sync(10,1,new() { AccountId=1, CurrentReading=current, Remarks="Normal Reading", ReadingDate=DateTime.Now });
            Check(valid && saved.CurrentReading-saved.PreviousReading == current-previous && saved.Remarks == "Normal Reading", "Valid equal/increasing reading was rejected or changed.");
        } catch (InvalidOperationException e) when (!valid) {
            Check(e.Message.Contains("previous reading"), "Decreasing reading failed for the wrong reason.");
        }
    });
await Test("Mobile sync rejects dates other than the intended day before billing", async () => {
    await SeedReaderAndAccount();
    var billingDate = (await db.ReadingSheets.SingleAsync()).BillingDate!.Value.Date;
    foreach (var date in new[] { billingDate, billingDate.AddDays(-2) }) {
        try {
            await readingService.Sync(10,1,new() { AccountId=1, CurrentReading=1, Remarks="Normal Reading", ReadingDate=date });
            throw new Exception("Invalid reading date was accepted.");
        } catch (ArgumentException e) { Check(e.Message == "Reading date must be one day before billing date.", "Wrong date rejection reason."); }
    }
    Check(!await db.Readings.AnyAsync(), "Rejected date wrote a reading.");
});
foreach (var water in new[] { 170m, 900m })
await Test($"Collection uses issued water/maintenance and applied-only penalty ({water})", async () => {
    await SeedReaderAndAccount();
    var rate = await db.WaterRates.SingleAsync(); rate.MinimumCharge = water;
    await db.SaveChangesAsync();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 0, ReadingDate = DateTime.Now });
    await sheetService.PartialPost(1, 1);
    db.ChangeTracker.Clear();
    var bill = await db.Billings.SingleAsync(); var frozen = bill.InvoiceSnapshotJson;
    var summaryService = new BillingService(db);
    var before = await summaryService.GetCollectionBill(bill.BillingReferenceId);
    Check(before?.BillAmount == water && before.MeterMaintenanceFee == 20m
        && before.AmountDue == water + 20m && before.Penalty == 0m
        && before.PenaltyAfterDue == water / 10m && before.TotalAfterDue == water + 20m + water / 10m
        && before.ServiceInvoiceNumber == bill.ServiceInvoiceNumber && before.PaymentReference == null,
        "Collection mixed snapshot components, identities or future penalty with payable balance.");
    rate = await db.WaterRates.SingleAsync(); rate.MinimumCharge = 12345m;
    await db.SaveChangesAsync();
    var again = await summaryService.GetCollectionBill(bill.BillingReferenceId);
    Check(JsonSerializer.Serialize(before) == JsonSerializer.Serialize(again)
        && await db.Payments.CountAsync() == 0 && await db.Penalties.CountAsync() == 0,
        "Collection lookup recalculated rates or wrote financial records.");
    var sheet = await db.ReadingSheets.SingleAsync(); sheet.DueDate = TMCWD.Model.Billing.LatePaymentPenalty.Today.AddDays(-1);
    await db.SaveChangesAsync();
    await penaltyService.PostCharges(1, new() { BillingReferenceIds = new() { bill.BillingReferenceId } });
    again = await summaryService.GetCollectionBill(bill.BillingReferenceId);
    Check(again?.AmountDue == water + 20m + water / 10m && again.RemainingAmount == water + 20m
        && again.Penalty == water / 10m, "Applied penalty was omitted or counted twice.");
    await new PaymentService(db).ProcessCollectionPayment(1, new() { InvoiceNumber = "COLLECTION-PAYMENT", Method = PaymentMethod.Cash,
        AmountTendered = again!.AmountDue, Bills = new() { new() { BillingReferenceId = bill.BillingReferenceId, PaidAmount = again.AmountDue } } });
    db.ChangeTracker.Clear();
    var paid = await summaryService.GetCollectionBill(bill.BillingReferenceId);
    Check(paid?.AmountDue == 0m && paid.RemainingAmount == 0m && paid.Penalty == 0m
        && paid.PaymentReference == (await db.Payments.SingleAsync()).PaymentReference && paid.ServiceInvoiceNumber == before!.ServiceInvoiceNumber
        && (await db.Billings.SingleAsync()).InvoiceSnapshotJson == frozen,
        "Collection payment lost identity, snapshot or settled balance.");
});
await Test("Issued mobile Service Invoice financial snapshot survives actual collection payment", async () => {
    await SeedReaderAndAccount();
    var account = await db.Accounts.SingleAsync(); account.MeterNumber = "SYNTHETIC-METER";
    var rate = await db.WaterRates.SingleAsync(); rate.MinimumCharge = 170m;
    await db.SaveChangesAsync();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 0, ReadingDate = DateTime.Now });
    await sheetService.PartialPost(1, 1);
    db.ChangeTracker.Clear();
    var bill = await db.Billings.SingleAsync();
    var frozen = bill.InvoiceSnapshotJson;
    var sheet = await db.ReadingSheets.SingleAsync(); sheet.DueDate = TMCWD.Model.Billing.LatePaymentPenalty.Today.AddDays(-1);
    await db.SaveChangesAsync();
    await penaltyService.PostCharges(1, new() { BillingReferenceIds = new() { bill.BillingReferenceId } });
    var controller = MobileController(pen: penaltyService);
    async Task<TMCWD.Model.Billing.Responses.MobileSoaDto> ReadInvoice() =>
        (TMCWD.Model.Billing.Responses.MobileSoaDto)((await controller.Soa(1, 1)).Result as Microsoft.AspNetCore.Mvc.OkObjectResult)!.Value!;
    string IssuedFields(TMCWD.Model.Billing.Responses.MobileSoaDto soa) => JsonSerializer.Serialize(new {
        soa.BillAmount, soa.MeterMaintenance, soa.PowerCostAdjustment, soa.ArrearsAmount, soa.OtherCharges,
        soa.BillingAdjustment, soa.TotalDue, soa.PenaltyAfterDueAmount, soa.TotalAfterDue, soa.ChargeLines,
        soa.PeriodFrom, soa.PeriodTo, soa.ServiceInvoiceNumber, soa.QrPayload, soa.MeterNumber });
    var before = await ReadInvoice();
    Check(before.BillAmount == 170m && before.MeterMaintenance == 20m && before.TotalDue == 190m
        && before.PenaltyAfterDueAmount == 17m && before.TotalAfterDue == 207m && before.CurrentPenaltyAmount == 17m
        && before.MeterNumber == "SYNTHETIC-METER", "Issued values or authoritative meter mapping are incorrect.");
    await new PaymentService(db).ProcessCollectionPayment(1, new() { Method = PaymentMethod.Cash, AmountTendered = 207m,
        Bills = new() { new() { BillingReferenceId = bill.BillingReferenceId, PaidAmount = 207m } } });
    db.ChangeTracker.Clear();
    var after = await ReadInvoice();
    Check(after.RemainingAmount == 0m && after.PaymentStatus == PaymentStatus.Paid && after.CurrentPenaltyAmount == 0m,
        "Regression did not exercise actual payment/penalty settlement.");
    Check(IssuedFields(after) == IssuedFields(before) && (await db.Billings.SingleAsync()).InvoiceSnapshotJson == frozen,
        "Actual collection payment or subsequent SOA GET changed issued invoice fields.");
});
await Test("Collection adjustment changes payable balance without inventing water or maintenance", async () => {
    await SeedReaderAndAccount();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 0, ReadingDate = DateTime.Now });
    await sheetService.PartialPost(1, 1);
    var bill = await db.Billings.SingleAsync();
    await new BillingAdjustmentService(db).SaveBatch(1, new() { new() {
        BillingReferenceId = bill.BillingReferenceId, Type = (int)AdjustmentLineType.CurrentBill, Amount = 30m } }, Guid.NewGuid());
    var summary = await new BillingService(db).GetCollectionBill(bill.BillingReferenceId);
    Check(summary?.BillAmount == 100m && summary.MeterMaintenanceFee == 20m && summary.BillingAdjustment == 30m
        && summary.AmountDue == 90m && summary.TotalDue == 120m,
        "Collection inferred water from an adjusted remaining balance or recollected the original total.");
});
await Test("Collection historical balance remains viewable and payable without fabricated components", async () => {
    var bill = await OverdueBill(); bill.InvoiceSnapshotJson = null;
    await db.SaveChangesAsync();
    var summary = await new BillingService(db).GetCollectionBill(bill.BillingReferenceId);
    Check(summary?.BillAmount == null && summary!.MeterMaintenanceFee == null && summary.PenaltyAfterDue == null
        && summary.AmountDue == 1000m, "Historical Collection inferred water charge or blocked legitimate principal.");
    await new PaymentService(db).ProcessCollectionPayment(1, new() { InvoiceNumber = "HISTORICAL-PRINCIPAL", Method = PaymentMethod.Cash,
        AmountTendered = 1000m, Bills = new() { new() { BillingReferenceId = bill.BillingReferenceId, PaidAmount = 1000m } } });
    Check((await db.Payments.SingleAsync()).PaidAmount == 1000m && (await db.Billings.SingleAsync()).InvoiceSnapshotJson == null,
        "Historical principal payment reconstructed a snapshot.");
});
await Test("Collection rejects partial, overpayment and duplicate submissions without mutations", async () => {
    var bill = await OverdueBill();
    var payments = new PaymentService(db);
    foreach (var amount in new[] { 100m, 1001m }) {
        await Reject(async () => await payments.ProcessCollectionPayment(1, new() { InvoiceNumber = "BAD-REQUEST", Method = PaymentMethod.Cash,
            AmountTendered = 2000m, Bills = new() { new() { BillingReferenceId = bill.BillingReferenceId, PaidAmount = amount } } }));
        db.ChangeTracker.Clear();
        Check(await db.Payments.CountAsync() == 0 && (await db.Billings.SingleAsync()).RemainingAmount == 1000m,
            "Rejected partial/overpayment changed a balance.");
    }
    var request = new TMCWD.Model.Billing.Requests.CollectionPaymentRequest { InvoiceNumber = "ONE-PAYMENT", Method = PaymentMethod.Cash,
        AmountTendered = 1000m, Bills = new() { new() { BillingReferenceId = bill.BillingReferenceId, PaidAmount = 1000m } } };
    await payments.ProcessCollectionPayment(1, request);
    await Reject(async () => await payments.ProcessCollectionPayment(1, request));
    Check(await db.Payments.CountAsync() == 1 && (await db.Billings.SingleAsync()).RemainingAmount == 0m,
        "Duplicate submission created another payment or negative balance.");
});
await Test("Collection payment failure rolls back payment and balance atomically", async () => {
    var bill = await OverdueBill();
    failure.Enabled = true;
    failure.PaymentsOnly = true;
    await Reject(async () => await new PaymentService(db).ProcessCollectionPayment(1, new() { InvoiceNumber = "ROLLBACK", Method = PaymentMethod.Cash,
        AmountTendered = 1000m, Bills = new() { new() { BillingReferenceId = bill.BillingReferenceId, PaidAmount = 1000m } } }));
    failure.Enabled = false; db.ChangeTracker.Clear();
    Check(await db.Payments.CountAsync() == 0 && (await db.Billings.SingleAsync()).RemainingAmount == 1000m,
        "Failed Collection transaction left partial financial writes.");
    Check(!await db.PaymentReceipts.AnyAsync(), "Failed payment left a committed receipt header.");
    failure.PaymentsOnly = false;
});
await Test("One receipt covers multiple bill lines with a shared authoritative cashier", async () => {
    db.Billings.AddRange(new[] { "MULTI-A", "MULTI-B" }.Select(reference => new Billing { BillingReferenceId = reference,
        AccountId = 1, TotalBillAmount = 100m, RemainingAmount = 100m, PaymentStatus = (int)PaymentStatus.Unpaid,
        BillingPeriod = DateTime.Today, DateCreated = DateTime.Now, DateUpdated = DateTime.Now }));
    await db.SaveChangesAsync();
    var lines = await new PaymentService(db).ProcessCollectionPayment(7, new() { Method = PaymentMethod.Cash,
        InvoiceNumber = "IGNORE-CLIENT", AmountTendered = 200m, Bills = new() {
            new() { BillingReferenceId = "MULTI-A", PaidAmount = 100m }, new() { BillingReferenceId = "MULTI-B", PaidAmount = 100m } } });
    Check(lines.Count == 2 && lines.Select(p => p.PaymentReference).Distinct().Count() == 1
        && lines.All(p => p.CreatedBy == 7 && p.PaymentReference != "IGNORE-CLIENT") && await db.PaymentReceipts.CountAsync() == 1,
        "Multi-bill payment created separate receipts or trusted browser reference.");
});
await Test("Cancellation restores exact linked charges independently of timestamps", async () => {
    db.Billings.Add(new Billing { BillingReferenceId = "LINKED", AccountId = 1, TotalBillAmount = 100m,
        RemainingAmount = 100m, PaymentStatus = (int)PaymentStatus.Unpaid, BillingPeriod = DateTime.Today,
        DateCreated = DateTime.Now, DateUpdated = DateTime.Now });
    db.Penalties.Add(new Penalty { BillingReferenceId = "LINKED", Amount = 10m, PaymentStatus = (int)PaymentStatus.Unpaid,
        DateCreated = DateTime.Now, DateUpdated = DateTime.Now });
    db.OtherCharges.Add(new OtherCharge { BillingReferenceId = "LINKED", Amount = 20m, PayableIn = "1", InstallmentCount = 1, PaymentStatus = (int)PaymentStatus.Unpaid,
        IsActive = true, DateCreated = DateTime.Now, DateUpdated = DateTime.Now });
    await db.SaveChangesAsync();
    var parent = await db.OtherCharges.SingleAsync();
    db.OtherChargeInstallments.Add(new OtherChargeInstallment { OtherChargeId = parent.Id, InstallmentNumber = 1, DueBillingPeriod = DateTime.Today, Amount = 20m, PaymentStatus = (int)PaymentStatus.Unpaid });
    await db.SaveChangesAsync();
    var childId = (await db.OtherChargeInstallments.SingleAsync()).Id;
    var service = new PaymentService(db);
    var payment = (await service.ProcessCollectionPayment(7, new() { Method = PaymentMethod.Cash, AmountTendered = 130m,
        Bills = new() { new() { BillingReferenceId = "LINKED", PaidAmount = 130m, InstallmentIds = new() { childId } } } })).Single();
    var penalty = await db.Penalties.SingleAsync(); var charge = await db.OtherChargeInstallments.SingleAsync();
    Check(penalty.SettledPaymentId == payment.Id && charge.SettledPaymentId == payment.Id, "Settlement did not link exact payment ID.");
    penalty.DateUpdated = DateTime.Today.AddDays(-5); charge.DateUpdated = DateTime.Today.AddDays(-6);
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    await service.CancelInvoice(7, new() { InvoiceNo = payment.PaymentReference, Reason = "Synthetic correction" });
    db.ChangeTracker.Clear();
    Check((await db.Billings.SingleAsync()).RemainingAmount == 100m && (await db.Penalties.SingleAsync()).PaymentStatus == (int)PaymentStatus.Unpaid
        && (await db.OtherCharges.SingleAsync()).PaymentStatus == (int)PaymentStatus.Unpaid && !await db.Payments.AnyAsync(p => p.Success),
        "Cancellation depended on timestamp precision or restored penalty into principal.");
});
await Test("Legacy penalty creation uses authoritative posting validation, not caller amounts", async () => {
    await SeedReaderAndAccount();
    await readingService.Sync(10, 1, new() { AccountId = 1, CurrentReading = 0, ReadingDate = DateTime.Now });
    await sheetService.PartialPost(1, 1);
    var bill = await db.Billings.SingleAsync();
    var sheet = await db.ReadingSheets.SingleAsync();
    sheet.DueDate = TMCWD.Model.Billing.LatePaymentPenalty.Today;
    await db.SaveChangesAsync();
    await Reject(async () => await penaltyService.SaveUpdate(1, new() { BillingReferenceId = bill.BillingReferenceId, Amount = 999m }));
    Check(!await db.Penalties.AnyAsync(), "Legacy route charged on the due date.");
    sheet.DueDate = TMCWD.Model.Billing.LatePaymentPenalty.Today.AddDays(-1);
    await db.SaveChangesAsync();
    var posted = await penaltyService.SaveUpdate(1, new() { BillingReferenceId = bill.BillingReferenceId, Amount = 999m });
    Check(posted.Amount == 10m, "Legacy route trusted a caller penalty amount.");
    await Reject(async () => await penaltyService.SaveUpdate(1, new() { Id = posted.Id, BillingReferenceId = bill.BillingReferenceId, Amount = 999m }));
    Check((await db.Penalties.SingleAsync()).Amount == 10m, "Legacy route rewrote a posted penalty.");
});
await OtherChargeInstallmentsRegression.Run(db, sheetService, Test);
await AccountLedgerRegression.Run(db, Test);
if (args.Contains("--verify-disconnection-workflow", StringComparer.Ordinal))
    await DisconnectionWorkflowRegression.Run(db, Test, enabled => failure.Enabled = enabled);
if (args.Contains("--verify-development-deployment", StringComparer.Ordinal))
    await DevelopmentDeploymentVerification.Run(db, sheetService, readingService, Test);
Console.WriteLine($"{passed} database regression checks passed using temporary tables only.");
if (runMigrationChecks)
{
await InvoiceConcurrency.Run(connectionOptions, Path.Combine(root,
    "TMCWD/TMCWD.Data/Database/20261006_add_service_invoice_bir_config.sql"));
await PaymentConcurrency.Run(connectionOptions, root);
}
else
{
    Console.WriteLine("NOT RUN (--no-migrations): migration SQL and invoice/payment concurrency suites that execute migrations.");
}

if (args.Contains("--verify-adjustment-workflow", StringComparer.Ordinal))
{
    if (!await AdjustmentWorkflowVerification.Run(db, sheetService, readingService, Test, enabled => failure.Enabled = enabled))
        Environment.ExitCode = 1;
    if (runMigrationChecks) await AdjustmentConcurrency.Run(connectionOptions, root);
}

if (args.Contains("--verify-disconnection-workflow", StringComparer.Ordinal) && runMigrationChecks)
    await DisconnectionConcurrency.Run(connectionOptions, root);
if (development6JBefore != null) {
    Check(await DisconnectionWorkflowRegression.InspectDevelopment() == development6JBefore, "Development baseline or schema changed during Phase 6J tests.");
    Console.WriteLine("PASS: Phase 6J development counts, sums, issued snapshot digests and inspected schemas unchanged.");
}

sealed class FailAfterSave : SaveChangesInterceptor
{
    public bool Enabled { get; set; }
    public bool PaymentsOnly { get; set; }
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (Enabled && (!PaymentsOnly || eventData.Context!.ChangeTracker.Entries<Payment>().Any(p => p.Entity.Success))) throw new InvalidOperationException("Injected failure after SQL save and before transaction commit.");
        return ValueTask.FromResult(result);
    }
}

