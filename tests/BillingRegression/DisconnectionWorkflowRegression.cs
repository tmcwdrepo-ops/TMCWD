using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;
using TMCWD.Model.Billing;
using TMCWD.Model.Billing.Requests;
using Billing = TMCWD.Data.Entities.Billing;
using Penalty = TMCWD.Data.Entities.Penalty;
using Reading = TMCWD.Data.Entities.Reading;
using ReadingSheet = TMCWD.Data.Entities.ReadingSheet;
using OtherCharge = TMCWD.Data.Entities.OtherCharge;

static class DisconnectionWorkflowRegression
{
    public static async Task<string> InspectDevelopment()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft/UserSecrets/TMCWD.Data-local-development/secrets.json");
        using var secrets = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var settings = new MySqlConnector.MySqlConnectionStringBuilder(secrets.RootElement.GetProperty("ConnectionStrings:DefaultConnection").GetString()
            ?? throw new InvalidOperationException("Development inspection requires a configured connection."));
        if (settings.Server != "127.0.0.1" || settings.Port != 3307 || settings.Database != "tmcwd") throw new Exception("Unexpected development identity.");
        await using var connection = new MySqlConnector.MySqlConnection(settings.ConnectionString);
        await connection.OpenAsync();
        await using (var begin = new MySqlConnector.MySqlCommand("START TRANSACTION WITH CONSISTENT SNAPSHOT, READ ONLY", connection)) await begin.ExecuteNonQueryAsync();
        var evidence = new List<string>();
        foreach (var sql in new[] {
            "SELECT CONCAT_WS('|',DATABASE(),@@port,@@server_uuid,VERSION())",
            "SELECT CONCAT_WS('|',(SELECT COUNT(*) FROM Billings),(SELECT COUNT(*) FROM readings),(SELECT COUNT(*) FROM reading_sheets),(SELECT COUNT(*) FROM payments),(SELECT COUNT(*) FROM penalties),(SELECT COUNT(*) FROM other_charges),(SELECT COUNT(*) FROM other_charge_installments),(SELECT COUNT(*) FROM Billings WHERE InvoiceSnapshotJson IS NOT NULL))",
            "SELECT CONCAT_WS('|',SUM(TotalBillAmount),SUM(RemainingAmount),SUM(BillArrearsAmount),SUM(BillingAdjustment)) FROM Billings",
            "SELECT CONCAT_WS('|',TABLE_NAME,COLUMN_NAME,COLUMN_TYPE,IS_NULLABLE) FROM information_schema.columns WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('disconnection_orders','service_disconnection_events','billing_adjustment_operations') ORDER BY TABLE_NAME,ORDINAL_POSITION",
            "SELECT CONCAT_WS('|',Id,SHA2(InvoiceSnapshotJson,256)) FROM Billings WHERE InvoiceSnapshotJson IS NOT NULL ORDER BY Id",
            "SELECT CONCAT_WS('|',Id,Name,DefaultValue) FROM other_fee_types WHERE Name='Reconnection Fee' ORDER BY Id",
            "SELECT CONCAT_WS('|','account-state',Id,SHA2(CONCAT_WS('|',Status,UpdatedBy,DateUpdated),256)) FROM accounts ORDER BY Id"
        }) {
            await using var command = new MySqlConnector.MySqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) evidence.Add(reader.GetString(0));
        }
        if (evidence.Any(e => e.StartsWith("service_disconnection_events|", StringComparison.Ordinal))) {
            await using var events = new MySqlConnector.MySqlCommand("SELECT CONCAT_WS('|','physical-event',Id,SHA2(CONCAT_WS('|',AccountId,DisconnectedBy,ActualDisconnectionDateUtc,ReconnectedBy,ActualReconnectionDateUtc,ReconnectionFeeOtherChargeId),256)) FROM service_disconnection_events ORDER BY Id", connection);
            await using var reader = await events.ExecuteReaderAsync();
            while (await reader.ReadAsync()) evidence.Add(reader.GetString(0));
        }
        Check(evidence[0] == "tmcwd|3307|c3f950fe-b565-11f1-b6a6-30560f67a354|8.0.46", "Development server identity changed.");
        Console.WriteLine("READ ONLY Phase 6J development identity/counts/sums: " + string.Join("; ", evidence.Take(3)));
        var json = System.Text.Json.JsonSerializer.Serialize(evidence);
        await File.WriteAllTextAsync(".tmp/phase6j-development-inspection.json", json);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)));
    }

    public static async Task Run(UserDbContext db, Func<string, Func<Task>, Task> test, Action<bool> injectFailure)
    {
        var service = new DisconnectionMonitoringService(db, new BillingService(db));
        var billing = new BillingService(db);
        var payments = new PaymentService(db);
        var day0 = new DateOnly(2026, 11, 8);
        var utc = new DateTime(2026, 11, 13, 2, 0, 0, DateTimeKind.Utc);
        const string snapshot = "{\"BillAmount\":170,\"MeterMaintenance\":20,\"TotalDue\":9999}";
        async Task Seed(bool second = true, decimal firstAmount = 190) {
            var account = await db.Accounts.SingleAsync(); account.Status = (int)AccountStatus.Active;
            var fee = await db.OtherFeeTypes.SingleAsync(); fee.Name = "Reconnection Fee"; fee.DefaultValue = 999;
            var sheet = await db.ReadingSheets.SingleAsync(); sheet.BillingDate = new DateTime(2026, 10, 6); sheet.DueDate = new DateTime(2026, 12, 25);
            sheet.DisconnectionDate = new DateTime(2099, 1, 1);
            db.Readings.Add(new Reading { Id = 101, AccountId = 1, ReadingSheetId = 1, Status = ReadingStatus.Completed, DateCreated = new DateTime(2026, 10, 5) });
            db.Billings.Add(new Billing { Id = 500, AccountId = 1, ReadingId = 101, BillingReferenceId = "J-FIRST",
                BillingPeriod = new DateTime(2026, 10, 6), RemainingAmount = firstAmount, TotalBillAmount = 190,
                PaymentStatus = firstAmount == 0 ? (int)PaymentStatus.Paid : (int)PaymentStatus.Unpaid,
                InvoiceSnapshotJson = snapshot, ServiceInvoiceNumber = "J-SI1", DateCreated = utc, DateUpdated = utc });
            if (second) {
                db.ReadingSheets.Add(new ReadingSheet { Id = 2, ZoneBookId = 1, Status = ReadingStatus.InProgress,
                    BillingDate = new DateTime(2026, 11, 8), DueDate = new DateTime(2026, 11, 24), DisconnectionDate = new DateTime(2099, 1, 1) });
                db.Readings.Add(new Reading { Id = 102, AccountId = 1, ReadingSheetId = 2, Status = ReadingStatus.Completed, DateCreated = new DateTime(2026, 11, 7) });
                db.Billings.Add(new Billing { Id = 100, AccountId = 1, ReadingId = 102, BillingReferenceId = "J-SECOND",
                    BillingPeriod = new DateTime(2026, 11, 8), RemainingAmount = 207, TotalBillAmount = 207, PaymentStatus = (int)PaymentStatus.Unpaid,
                    InvoiceSnapshotJson = snapshot, ServiceInvoiceNumber = "J-SI2", DateCreated = utc, DateUpdated = utc });
            }
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        }
        async Task<DisconnectionMonitoringDto> Get(int days = 5) => await service.Get("TEST", day0.AddDays(days)) ?? throw new Exception("Account missing.");
        async Task<ServiceDisconnectionEvent> Disconnect(Guid? key = null) => await service.ConfirmDisconnection("TEST", key ?? Guid.NewGuid(), 1, utc, day0.AddDays(5));
        async Task<string> Pay(string? reference = null) {
            var refs = await db.Billings.Where(b => b.AccountId == 1 && (reference == null || b.BillingReferenceId == reference)).Select(b => b.BillingReferenceId).ToListAsync();
            var request = new CollectionPaymentRequest { Method = PaymentMethod.Cash };
            foreach (var r in refs) {
                var c = await billing.GetCollectionBill(r) ?? throw new Exception("Bill missing.");
                if (c.AmountDue > 0) request.Bills.Add(new() { BillingReferenceId = r, PaidAmount = c.AmountDue,
                    InstallmentIds = c.CollectibleOtherCharges.Select(i => i.InstallmentId).ToList() });
            }
            request.AmountTendered = request.Bills.Sum(b => b.PaidAmount);
            var receipt = (await payments.ProcessCollectionPayment(1, request)).First().PaymentReference;
            db.ChangeTracker.Clear(); return receipt;
        }
        async Task Reject(Func<Task> action) {
            try { await action(); } catch (InvalidOperationException) { db.ChangeTracker.Clear(); return; }
            throw new Exception("Invalid transition accepted.");
        }
        async Task Case(string name, Func<Task> action) => await test("Phase 6J " + name, async () => { await Seed(); await action(); });
        await test("Phase 6J A: first unpaid bill only", async () => { await Seed(false); Check((await Get()).Status == "FIRST BILL UNPAID", "Second period invented."); });
        await test("Phase 6J B/I: first settled before second issuance", async () => { await Seed(firstAmount: 0); Check((await Get()).Status == "FIRST BILL UNPAID" && (await Get()).SecondBillingReference == null, "Paid first bill triggered disconnection."); });
        foreach (var days in new[] { 0, 4, 5, 6 })
            await Case($"C-G: second issuance +{days} days (before due)", async () => {
                var x = await Get(days);
                Check(x.Status == (days < 5 ? "SUBJECT FOR DISCONNECTION" : "FOR DISCONNECTION")
                    && x.SubjectDate == new DateTime(2026, 11, 8) && x.ScheduledDisconnectionDate == new DateTime(2026, 11, 13)
                    && x.DaysRemaining == Math.Max(0, 5 - days) && !x.ActualDisconnectionDateUtc.HasValue && x.PhysicalServiceStatus == "NO CONFIRMED EVENT",
                    "Issuance/Day-5 clock or physical boundary incorrect.");
                Check(!await db.ServiceDisconnectionEvents.AnyAsync() && !await db.OtherCharges.AnyAsync(), "GET created physical event or fee.");
                Check((await db.Accounts.SingleAsync()).Status == (int)AccountStatus.Active, "Calendar changed operational status.");
            });
        await Case("H: adjusted RemainingAmount is authoritative", async () => {
            await new BillingAdjustmentService(db).SaveBatch(1, [new() { BillingReferenceId = "J-FIRST", Type = 1, Amount = 30 }], Guid.NewGuid());
            Check((await Get()).FirstBillingRemainingAmount == 160 && (await Get()).Status == "FOR DISCONNECTION", "Original/snapshot amount used.");
        });
        await Case("J/K: payment then cancellation reevaluates eligibility", async () => {
            var receipt = await Pay("J-FIRST"); Check((await Get()).SubjectDate == null, "Settled first still qualifies.");
            await Reject(async () => await Disconnect());
            await payments.CancelInvoice(1, new() { InvoiceNo = receipt, Reason = "Synthetic 6J cancellation" }); db.ChangeTracker.Clear();
            Check((await Get()).Status == "FOR DISCONNECTION" && (await Get()).FirstBillingRemainingAmount == 190, "Cancellation was not reevaluated.");
        });
        await Case("L-M: penalties/charges are not monthly bills", async () => {
            await db.Billings.Where(b => b.Id == 100).ExecuteDeleteAsync();
            db.Penalties.Add(new Penalty { BillingReferenceId = "J-FIRST", Amount = 17, PaymentStatus = (int)PaymentStatus.Unpaid, DateCreated = utc });
            db.OtherCharges.Add(new OtherCharge { Id = 70, Type = 1, BillingReferenceId = "J-FIRST", Amount = 1000,
                PayableIn = "4", InstallmentCount = 4, PaymentStatus = (int)PaymentStatus.Unpaid, DateCreated = utc, DateUpdated = utc });
            for (var n = 1; n <= 4; ++n) db.OtherChargeInstallments.Add(new() { Id = 700 + n, OtherChargeId = 70, InstallmentNumber = n,
                Amount = 250, DueBillingPeriod = new DateTime(2026, 10, 1).AddMonths(n - 1),
                PaymentStatus = (int)(n == 1 ? PaymentStatus.Unpaid : PaymentStatus.Draft), ActivatedAt = n == 1 ? utc : null,
                ActivatedBillingReferenceId = n == 1 ? "J-FIRST" : null, DateCreated = utc, DateUpdated = utc });
            await db.SaveChangesAsync();
            Check((await Get()).Status == "FIRST BILL UNPAID" && (await Get()).TotalCollectibleBalance == 457, "Ancillary obligations counted as months.");
        });
        await Case("N: maintenance counted once", async () => Check((await Get()).TotalCollectibleBalance == 397, "Maintenance/arrears duplicated."));
        await Case("O-Q: explicit event and one exact 350 fee; replay preserves audit", async () => {
            var key = Guid.NewGuid(); var first = await Disconnect(key); db.ChangeTracker.Clear();
            var replay = await Disconnect(key);
            Check(first.Id == replay.Id && replay.DisconnectedBy == 1 && replay.ActualDisconnectionDateUtc == utc
                && await db.ServiceDisconnectionEvents.CountAsync() == 1 && await db.OtherCharges.CountAsync() == 1
                && (await db.OtherCharges.SingleAsync()).Amount == 350 && (await db.OtherChargeInstallments.SingleAsync()).Amount == 350,
                "Event/audit/fee duplicated or client/default fee accepted.");
            await Reject(async () => await Disconnect());
            await Reject(async () => await service.ConfirmDisconnection("TEST", key, 2, utc, day0.AddDays(5)));
            var x = await Get(); Check(x.Status == "BALANCE + RECONNECTION FEE PENDING" && x.PhysicalServiceStatus == "DISCONNECTED" && x.TotalCollectibleBalance == 747 && x.ReconnectionFeeBalance == 350, "Fee not immediately collectible exactly once.");
            Check(await db.Billings.AllAsync(b => b.InvoiceSnapshotJson == snapshot && b.BillingAdjustment == 0)
                && (await db.Accounts.SingleAsync()).Status == (int)AccountStatus.Suspended, "Financial snapshot or physical operational status incorrect.");
            var ledger = await new AccountLedgerService(db, billing).Get("TEST");
            Check(ledger?.Account.Status == "Disconnected" && ledger.Account.OperationalAccountStatus == "Suspended", "Ledger still displays Active after confirmation.");
        });
        await Case("R-T: each unpaid obligation blocks reconnection", async () => {
            await Disconnect(); await Reject(async () => await service.ConfirmReconnection("TEST", Guid.NewGuid(), 1, utc, day0.AddDays(5)));
            await Pay("J-FIRST"); Check((await Get()).Status != "ELIGIBLE FOR RECONNECTION", "Unpaid water/fee bypassed gate.");
            await Pay("J-SECOND"); Check((await Get()).Status == "ELIGIBLE FOR RECONNECTION", "Fully paid account blocked.");
        });
        await Case("S: water settled but fee unpaid", async () => {
            await Disconnect();
            await db.Billings.ExecuteUpdateAsync(s => s.SetProperty(b => b.RemainingAmount, 0).SetProperty(b => b.PaymentStatus, (int)PaymentStatus.Paid));
            Check((await Get()).Status == "BALANCE + RECONNECTION FEE PENDING" && (await Get()).TotalCollectibleBalance == 350, "Water settlement removed fee.");
            await Reject(async () => await service.ConfirmReconnection("TEST", Guid.NewGuid(), 1, utc, day0.AddDays(5)));
        });
        await Case("6J-F: paid linked fee cannot bypass another unpaid water obligation", async () => {
            await Disconnect(); await Pay("J-SECOND");
            Check((await Get()).ReconnectionFeeBalance == 0 && (await Get()).TotalCollectibleBalance > 0, "Fixture did not isolate paid fee with outstanding water.");
            await Reject(async () => await service.ConfirmReconnection("TEST", Guid.NewGuid(), 1, utc, day0.AddDays(5)));
            Check((await db.Accounts.SingleAsync()).Status == (int)AccountStatus.Suspended, "Fee-only settlement restored service.");
        });
        await Case("U-W: settlement gives eligibility; explicit reconnection and replay", async () => {
            await Disconnect(); await Pay(); var state = await Get();
            Check(state.Status == "ELIGIBLE FOR RECONNECTION" && state.ActualReconnectionDateUtc == null
                && (await db.Accounts.SingleAsync()).Status == (int)AccountStatus.Suspended, "Payment automatically reactivated/reconnected.");
            var financialBefore = await new AccountLedgerService(db, billing).Get("TEST") ?? throw new Exception("Ledger missing.");
            var key = Guid.NewGuid(); var first = await service.ConfirmReconnection("TEST", key, 2, utc.AddHours(1), day0.AddDays(5));
            db.ChangeTracker.Clear(); var replay = await service.ConfirmReconnection("TEST", key, 2, utc.AddHours(2), day0.AddDays(5));
            Check(first.Id == replay.Id && replay.ReconnectedBy == 2 && replay.ActualReconnectionDateUtc == utc.AddHours(1)
                && (await Get()).Status == "RECONNECTED" && await db.ServiceDisconnectionEvents.CountAsync() == 1, "Reconnection retry changed audit/event.");
            Check((await db.Accounts.SingleAsync()).Status == (int)AccountStatus.Active
                && (await new AccountLedgerService(db, billing).Get("TEST"))?.Account.Status == "Active", "Reconnection did not restore operational/Ledger status.");
            var financialAfter = await new AccountLedgerService(db, billing).Get("TEST") ?? throw new Exception("Ledger missing.");
            Check(financialAfter.TotalBalance == 0
                && System.Text.Json.JsonSerializer.Serialize(financialBefore.Bills) == System.Text.Json.JsonSerializer.Serialize(financialAfter.Bills)
                && System.Text.Json.JsonSerializer.Serialize(financialBefore.OtherCharges) == System.Text.Json.JsonSerializer.Serialize(financialAfter.OtherCharges)
                && System.Text.Json.JsonSerializer.Serialize(financialBefore.History) == System.Text.Json.JsonSerializer.Serialize(financialAfter.History),
                "Physical reconnection changed financial projection or removed paid fee/history.");
        });
        foreach (var reconnect in new[] { false, true })
            await Case("X/Y: cancellation " + (reconnect ? "after" : "before") + " physical reconnection", async () => {
                await Disconnect(); var receipt = await Pay();
                if (reconnect) await service.ConfirmReconnection("TEST", Guid.NewGuid(), 1, utc.AddHours(1), day0.AddDays(5));
                await payments.CancelInvoice(1, new() { InvoiceNo = receipt, Reason = "Synthetic 6J cancellation" }); db.ChangeTracker.Clear();
                var x = await Get(); Check(x.Status == (reconnect ? "FINANCIAL / SERVICE STATE REVIEW REQUIRED" : "BALANCE + RECONNECTION FEE PENDING")
                    && x.ActualReconnectionDateUtc.HasValue == reconnect && x.PhysicalServiceStatus == (reconnect ? "RECONNECTED" : "DISCONNECTED"), "Cancellation changed physical state or left stale eligibility.");
                Check((await db.Accounts.SingleAsync()).Status == (int)(reconnect ? AccountStatus.Active : AccountStatus.Suspended), "Cancellation reversed operational service state.");
            });
        await Case("6J-F: financially eligible account must remain Suspended before physical reconnection", async () => {
            await Disconnect(); await Pay();
            (await db.Accounts.SingleAsync()).Status = (int)AccountStatus.Active; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            await Reject(async () => await service.ConfirmReconnection("TEST", Guid.NewGuid(), 1, utc, day0.AddDays(5)));
            Check((await db.ServiceDisconnectionEvents.SingleAsync()).ActualReconnectionDateUtc == null, "Inconsistent operational account was reconnected.");
        });
        await Case("Z: same customer multiple account isolation", async () => {
            db.Accounts.Add(new Account { Id = 2, CustomerId = 1, AccountNumber = "OTHER", ZoneBookId = 1, Status = (int)AccountStatus.Active });
            await db.SaveChangesAsync(); await Disconnect();
            var x = await service.Get("OTHER", day0.AddDays(5)); Check(x?.Status == "CURRENT" && x.EventId == null && x.TotalCollectibleBalance == 0, "Customer-level state leaked.");
            Check(await service.Get("MISSING", day0) == null, "Unknown account fallback.");
        });
        await Case("nonconsecutive monthly periods do not qualify", async () => {
            await db.Billings.Where(b => b.Id == 500).ExecuteUpdateAsync(s => s.SetProperty(b => b.BillingPeriod, new DateTime(2026, 9, 6)));
            Check((await Get()).SubjectDate == null, "A skipped month triggered disconnection.");
        });
        await Case("duplicate monthly bill fails safely", async () => {
            db.Billings.Add(new Billing { Id = 900, AccountId = 1, ReadingId = 101, BillingReferenceId = "DUP", BillingPeriod = new DateTime(2026, 10, 20), RemainingAmount = 1 });
            await db.SaveChangesAsync(); Check((await Get()).FinancialReviewRequired, "Duplicate month selected arbitrarily."); await Reject(async () => await Disconnect());
        });
        await Case("deleted sheet and charge anchor do not qualify", async () => {
            await db.ReadingSheets.Where(s => s.Id == 2).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ReadingStatus.Deleted));
            Check((await Get()).SubjectDate == null, "Deleted sheet counted.");
            await db.ReadingSheets.Where(s => s.Id == 2).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ReadingStatus.InProgress));
            await db.Billings.Where(b => b.Id == 100).ExecuteUpdateAsync(s => s.SetProperty(b => b.ReadingId, 0));
            Check((await Get()).SubjectDate == null, "Charge anchor counted as water month.");
        });
        await Case("rollback after fee SQL preserves all event/financial state", async () => {
            injectFailure(true); await Reject(async () => await Disconnect()); injectFailure(false);
            Check(!await db.ServiceDisconnectionEvents.AnyAsync() && !await db.OtherCharges.AnyAsync() && !await db.OtherChargeInstallments.AnyAsync()
                && (await Get()).TotalCollectibleBalance == 397, "Partial event or fee survived rollback.");
            Check((await db.Accounts.SingleAsync()).Status == (int)AccountStatus.Active, "Failed fee creation suspended the account.");
        });
        await Case("Collections and Ledger reconciliation with penalties/future installments", async () => {
            db.Penalties.Add(new Penalty { BillingReferenceId = "J-FIRST", Amount = 17, PaymentStatus = (int)PaymentStatus.Unpaid, DateCreated = utc });
            await db.SaveChangesAsync();
            var charges = new OtherChargeService(db);
            var plan = await charges.SaveUpdate(1, new() { BillingReferenceId = "J-FIRST", Type = 1, Amount = 1000, PayableIn = "4" });
            await charges.PostToCollection(1, [(int)plan.Id]);
            await Disconnect(); var x = await Get();
            var ledger = await new AccountLedgerService(db, billing).Get("TEST");
            Check(x.TotalCollectibleBalance == 1014 && ledger?.TotalBalance == 1014 && x.CollectibleOtherCharges == 250
                && x.ReconnectionFeeBalance == 350 && x.ActivePenaltyBalance == 17, "1014 example/child-only reconciliation failed.");
            await Pay(); Check((await Get()).Status == "ELIGIBLE FOR RECONNECTION", "Future installments block reconnection.");
            Check(await db.OtherChargeInstallments.CountAsync(i => i.PaymentStatus == (int)PaymentStatus.Draft) == 3, "Future installments mutated.");
        });
        await Case("day 4 confirmation rejected and no fee", async () => {
            await Reject(async () => await service.ConfirmDisconnection("TEST", Guid.NewGuid(), 1, utc, day0.AddDays(4)));
            Check(!await db.ServiceDisconnectionEvents.AnyAsync() && !await db.OtherCharges.AnyAsync(), "Early physical confirmation wrote state.");
        });
        await Case("later service cycle creates one new event/fee, retaining old replay", async () => {
            var oldKey = Guid.NewGuid(); await Disconnect(oldKey); await Pay();
            await service.ConfirmReconnection("TEST", Guid.NewGuid(), 1, utc.AddHours(1), day0.AddDays(5));
            for (var n = 3; n <= 4; ++n) {
                var date = new DateTime(2026, 12, 8).AddMonths(n - 3);
                db.ReadingSheets.Add(new ReadingSheet { Id = n, ZoneBookId = 1, Status = ReadingStatus.InProgress, BillingDate = date });
                db.Readings.Add(new Reading { Id = n, AccountId = 1, ReadingSheetId = n, Status = ReadingStatus.Completed, DateCreated = date.AddDays(-1) });
                db.Billings.Add(new Billing { Id = n, AccountId = 1, ReadingId = n, BillingReferenceId = "LATER" + n,
                    BillingPeriod = date, RemainingAmount = 190, TotalBillAmount = 190, PaymentStatus = (int)PaymentStatus.Unpaid, InvoiceSnapshotJson = snapshot });
            }
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var laterDate = new DateOnly(2027, 1, 13);
            Check((await service.Get("TEST", laterDate))?.Status == "FOR DISCONNECTION", "Past reconnection hides new qualifying months.");
            await service.ConfirmDisconnection("TEST", Guid.NewGuid(), 1, new DateTime(2027, 1, 13, 2, 0, 0, DateTimeKind.Utc), laterDate);
            var replay = await Disconnect(oldKey);
            Check(replay.Id == oldKey.ToString("D") && await db.ServiceDisconnectionEvents.CountAsync() == 2
                && await db.OtherCharges.CountAsync(c => c.Amount == 350) == 2, "Later cycle duplicated or replaced historical event.");
        });
        await Case("missing fee configuration rejects atomically", async () => {
            await db.OtherFeeTypes.ExecuteDeleteAsync(); await Reject(async () => await Disconnect());
            Check(!await db.OtherCharges.AnyAsync() && !await db.ServiceDisconnectionEvents.AnyAsync(), "Fee type was guessed or event committed without fee.");
            Check((await db.Accounts.SingleAsync()).Status == (int)AccountStatus.Active, "Missing fee type suspended account.");
        });
        await Case("legacy Suspended without an event is not Disconnected", async () => {
            await db.Accounts.ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, (int)AccountStatus.Suspended));
            var ledger = await new AccountLedgerService(db, billing).Get("TEST");
            Check(ledger?.Account.Status == "Suspended" && ledger.Account.PhysicalServiceStatus == "NO CONFIRMED EVENT"
                && (await Get()).FinancialReviewRequired, "Legacy suspension was inferred as physical disconnection.");
        });
        await Case("suspended reading block and restored Active reading eligibility", async () => {
            await Disconnect();
            db.ReadingSheets.Add(new ReadingSheet { Id = 3, ZoneBookId = 1, Status = ReadingStatus.InProgress, BillingDate = new DateTime(2026, 12, 8) });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var accounts = new AccountService(db); var readings = new ReadingService(db, accounts);
            var sheet = await db.ReadingSheets.SingleAsync(s => s.Id == 3);
            Check(!(await accounts.GetReadingSheetRoster(sheet)).Any(a => a.Id == 1)
                && (await accounts.Search("TEST")).Any(a => a.Id == 1), "Suspension did not isolate reading from Collections search.");
            var before = await db.Readings.CountAsync();
            await Reject(async () => await readings.SaveUpdate(1, new Reading { AccountId = 1, ReadingSheetId = 3, CurrentReading = 10, Status = ReadingStatus.InProgress }));
            try { await readings.Sync(1, 3, new MobileReadingUpload { AccountId = 1, CurrentReading = 10, Remarks = "", ReadingDate = new DateTime(2026, 12, 7) }, false); throw new Exception("Suspended mobile reading accepted."); }
            catch (ArgumentException) { }
            Check(await db.Readings.CountAsync() == before, "Rejected reading wrote data.");
            await Pay(); Check((await Get()).Status == "ELIGIBLE FOR RECONNECTION", "Suspended account could not settle obligations.");
            await service.ConfirmReconnection("TEST", Guid.NewGuid(), 1, utc.AddHours(1), day0.AddDays(5));
            Check((await accounts.GetReadingSheetRoster(sheet)).Any(a => a.Id == 1), "Reconnection did not restore active roster.");
            await readings.SaveUpdate(1, new Reading { AccountId = 1, ReadingSheetId = 3, CurrentReading = 10, Status = ReadingStatus.InProgress });
            Check(await db.Readings.CountAsync() == before + 1, "Reconnected account cannot receive normal reading.");
        });
        foreach (var reconnect in new[] { false, true })
            await Case("atomic final-save rollback: " + (reconnect ? "reconnection + Active" : "event + fee + Suspended"), async () => {
                if (reconnect) { await Disconnect(); await Pay(); }
                await using var failingDb = new UserDbContext(new DbContextOptionsBuilder<UserDbContext>()
                    .UseMySql(db.Database.GetDbConnection(), new MySqlServerVersion(new Version(8, 0, 46)))
                    .AddInterceptors(new FailPhysicalSave()).Options);
                var failing = new DisconnectionMonitoringService(failingDb, new BillingService(failingDb));
                await Reject(async () => {
                    if (reconnect) await failing.ConfirmReconnection("TEST", Guid.NewGuid(), 1, utc.AddHours(1), day0.AddDays(5));
                    else await failing.ConfirmDisconnection("TEST", Guid.NewGuid(), 1, utc, day0.AddDays(5));
                });
                db.ChangeTracker.Clear();
                Check((await db.Accounts.SingleAsync()).Status == (int)(reconnect ? AccountStatus.Suspended : AccountStatus.Active), "Rolled-back status survived.");
                if (reconnect) Check((await db.ServiceDisconnectionEvents.SingleAsync()).ActualReconnectionDateUtc == null, "Rolled-back reconnection timestamp survived.");
                else Check(!await db.ServiceDisconnectionEvents.AnyAsync() && !await db.OtherCharges.AnyAsync() && !await db.OtherChargeInstallments.AnyAsync(), "Rolled-back event/fee survived final SQL save.");
            });
    }
    sealed class FailPhysicalSave : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesCompletedEventData data, int result, CancellationToken cancellationToken = default) {
            if (data.Context!.ChangeTracker.Entries<ServiceDisconnectionEvent>().Any()) throw new InvalidOperationException("Injected failure after physical-event/account SQL save.");
            return ValueTask.FromResult(result);
        }
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
