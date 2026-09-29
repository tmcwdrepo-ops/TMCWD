using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MySqlConnector;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

// These pure calculation checks run even when the database is unavailable.
foreach (var (bill, expected) in new[] { (1000m, 100m), (245.55m, 24.56m), (0m, 0m), (-10m, 0m), (100.04m, 10m) })
{
    if (TMCWD.Model.Billing.LatePaymentPenalty.Calculate(bill) != expected)
        throw new Exception($"Incorrect 10% penalty calculation for {bill}.");
}
Console.WriteLine("PASS: 5 penalty calculation and currency-rounding cases.");

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
            new object?[] { period, TMCWD.Model.Billing.LatePaymentPenalty.Today })!;
        _ = query.ToQueryString();
    }
}
Console.WriteLine("PASS: both penalty eligibility queries translate to MySQL.");

// All writes are confined to connection-local TEMPORARY tables. Never run
// tests against persistent customer records, even in a development database.
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
if (args.Length > 0) root = Path.GetFullPath(args[0]);
string? connectionString = Environment.GetEnvironmentVariable("BILLING_TEST_CONNECTION");
foreach (var file in new[] {
    Path.Combine(root, "TMCWD/TMCWD.Data/appsettings.json"),
    Path.Combine(root, "TMCWD/TMCWD.Data/appsettings.Development.json"),
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft/UserSecrets/TMCWD.Data-local-development/secrets.json") })
{
    if (connectionString != null || !File.Exists(file)) continue;
    using var json = JsonDocument.Parse(File.ReadAllText(file));
    if (json.RootElement.TryGetProperty("ConnectionStrings", out var strings)
        && strings.TryGetProperty("DefaultConnection", out var value)) connectionString = value.GetString();
    if (json.RootElement.TryGetProperty("ConnectionStrings:DefaultConnection", out var flat)) connectionString = flat.GetString();
}
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("SKIPPED: configure BILLING_TEST_CONNECTION to run the temporary-table database checks.");
    Environment.ExitCode = 2;
    return;
}
var connectionOptions = new MySqlConnectionStringBuilder(connectionString);
if (!new[] { "localhost", "127.0.0.1", "::1" }.Contains(connectionOptions.Server))
    throw new Exception("Regression checks require a local MySQL server.");
connectionOptions.AllowUserVariables = true;
connectionOptions.UseAffectedRows = false;
await using var connection = new MySqlConnection(connectionOptions.ConnectionString);
await connection.OpenAsync();
var failure = new FailAfterSave();
var options = new DbContextOptionsBuilder<UserDbContext>()
    .UseMySql(connection, ServerVersion.AutoDetect(connection)).AddInterceptors(failure).Options;
await using var db = new UserDbContext(options);
var tables = new[] { typeof(Account), typeof(ReadingSheet), typeof(Reading), typeof(Billing), typeof(WaterRate), typeof(Penalty), typeof(Customer), typeof(Payment), typeof(TMCWD.Data.Entities.BillingAdjustment), typeof(User), typeof(PenaltyWaiverRequest) }
    .Select(t => db.Model.FindEntityType(t)!.GetTableName()!)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
foreach (var table in tables)
{
    // Table identifiers come exclusively from the application's EF model.
    var database = connection.Database.Replace("`", "``", StringComparison.Ordinal);
    string createSql;
    await using (var showCommand = connection.CreateCommand())
    {
        showCommand.CommandText = $"SHOW CREATE TABLE `{database}`.`{table}`";
        await using var reader = await showCommand.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new Exception($"Could not inspect table {table}.");
        createSql = reader.GetString(1);
    }
    createSql = createSql.Replace("CREATE TABLE", "CREATE TEMPORARY TABLE", StringComparison.OrdinalIgnoreCase);
    await using var createCommand = connection.CreateCommand();
    createCommand.CommandText = createSql;
    await createCommand.ExecuteNonQueryAsync();
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
tables = tables.Append("senior_citizen_discounts").Append("invoice_cancellations").ToArray();
var readingService = new ReadingService(db);
var sheetService = new ReadingSheetService(db, new BillingService(db), new WaterRateService(db));
var penaltyService = new PenaltyService(db);
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
    db.ReadingSheets.Add(new ReadingSheet { Id = 1, ZoneBookId = 1, Status = ReadingStatus.InProgress, BillingDate = now.AddDays(2), DueDate = now.AddDays(10), BillingPeriodStart = now });
    db.WaterRates.Add(new WaterRate { Id = 1, Classification = 1, MeterSize = .5m, MinimumCharge = 100, Rate11To20 = 10, Rate21To30 = 20, Rate31To40 = 30, Rate41Up = 40, IsActive = true, EffectiveDate = now, DateCreated = now, DateUpdated = now });
    db.Users.AddRange(
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
async Task<Billing> OverdueBill()
{
    var sheet = await db.ReadingSheets.SingleAsync();
    sheet.DueDate = TMCWD.Model.Billing.LatePaymentPenalty.Today.AddDays(-1);
    await db.SaveChangesAsync();
    var reading = await readingService.SaveUpdate(1, NewReading());
    var bill = new Billing { AccountId = 1, ReadingId = reading.Id, BillingReferenceId = "OVERDUE",
        BillingPeriod = sheet.BillingDate!.Value, TotalBillAmount = 1000, RemainingAmount = 1000,
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
await Test("Adjusted bill collects the remaining balance with one maintenance fee", async () => {
    await readingService.SaveUpdate(1, NewReading(0));
    Check(await sheetService.PartialPost(1, 1) == 1, "Bill was not posted.");
    var bill = await db.Billings.SingleAsync();
    Check(bill.TotalBillAmount == 120 && bill.RemainingAmount == 120,
        "The original bill must contain the ₱100 minimum plus one ₱20 maintenance fee.");

    var adjustmentService = new BillingAdjustmentService(db);
    await adjustmentService.SaveBatch(1, new()
    {
        new TMCWD.Data.Entities.BillingAdjustment
        {
            BillingReferenceId = bill.BillingReferenceId,
            Type = (int)AdjustmentLineType.CurrentBill,
            Amount = 30
        }
    });
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
            InvoiceNo = "CANCEL-ME",
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
    Check(await db.InvoiceCancellations.AnyAsync(x => x.PaymentReference == "CANCEL-ME"
            && x.Reason == "Wrong amount"),
        "Invoice cancellation audit record is missing.");
    Check(await db.BillingAdjustments.AnyAsync(x =>
            x.Type == (int)AdjustmentLineType.SeniorCitizenDiscountReversal && x.Amount == -20),
        "Discount reversal adjustment is missing.");
});
Console.WriteLine($"{passed} database regression checks passed using temporary tables only.");

sealed class FailAfterSave : SaveChangesInterceptor
{
    public bool Enabled { get; set; }
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (Enabled) throw new InvalidOperationException("Injected failure after SQL save and before transaction commit.");
        return ValueTask.FromResult(result);
    }
}
