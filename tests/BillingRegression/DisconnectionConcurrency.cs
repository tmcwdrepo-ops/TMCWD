using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MySqlConnector;
using System.Text.RegularExpressions;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;
using TMCWD.Model.Billing.Requests;

static class DisconnectionConcurrency
{
    public static async Task Run(MySqlConnectionStringBuilder settings, string root)
    {
        if (settings.Database != "tmcwd_test" || !new[] { "localhost", "127.0.0.1", "::1" }.Contains(settings.Server))
            throw new Exception("Phase 6J concurrency requires local tmcwd_test.");
        var prefix = "tm_6j_" + Guid.NewGuid().ToString("N")[..16] + "_";
        string[] tables = ["Billings", "accounts", "customers", "zones_books", "readings", "reading_sheets", "service_disconnection_events",
            "other_charges", "other_charge_installments", "other_fee_types", "penalties", "payments", "payment_receipts", "users",
            "invoice_cancellations", "senior_citizen_discounts", "collection_remittances", "billing_adjustments", "water_connection_applications"];
        var rename = new SyntheticInvoiceTables(prefix, tables);
        DbContextOptions<UserDbContext> Options(PauseSave? pause = null) {
            var builder = new DbContextOptionsBuilder<UserDbContext>().UseMySql(settings.ConnectionString,
                new MySqlServerVersion(new Version(8, 0, 46))).AddInterceptors(rename);
            if (pause != null) builder.AddInterceptors(pause);
            return builder.Options;
        }
        var today = new DateOnly(2026, 11, 13);
        var utc = new DateTime(2026, 11, 13, 2, 0, 0, DateTimeKind.Utc);
        await using var setup = new UserDbContext(Options()); var created = new List<string>();
        try {
            foreach (var table in tables.Where(t => t != "service_disconnection_events")) {
                var ddl = Regex.Match(setup.Database.GenerateCreateScript(), $@"CREATE TABLE `{table}`[\s\S]*?;").Value;
                Require(ddl.Length > 0, "Missing owned fixture schema.");
                await setup.Database.ExecuteSqlRawAsync(ddl); created.Add(table);
            }
            var migration = await File.ReadAllTextAsync(Path.Combine(root, "TMCWD/TMCWD.Data/Database/20261012_add_service_disconnection_events.sql"));
            await setup.Database.ExecuteSqlRawAsync(migration); created.Add("service_disconnection_events");
            await setup.Database.ExecuteSqlRawAsync(migration);
            Console.WriteLine("PASS: Phase 6J additive event migration executed twice on owned tmcwd_test tables.");
            async Task Seed() {
                setup.ChangeTracker.Clear();
                foreach (var table in tables) await setup.Database.ExecuteSqlRawAsync("DELETE FROM `" + table + "`");
                setup.Accounts.Add(new Account { Id = 1, CustomerId = 1, AccountNumber = "RACE", ZoneBookId = 1, Status = (int)AccountStatus.Active });
                setup.Customers.Add(new Customer { Id = 1, Firstname = "Synthetic", Lastname = "Race" });
                setup.ZoneBooks.Add(new ZoneBook { Id = 1, Zone = 1, Book = 1 });
                setup.Users.Add(new User { Id = 1, Role = 7, IsActive = true, IsVerified = true });
                setup.Users.AddRange(new User { Id = 2, Role = 6, IsActive = true, IsVerified = true }, new User { Id = 3, Role = 1, IsActive = true, IsVerified = true });
                setup.OtherFeeTypes.Add(new OtherFeeType { Id = 1, Name = "Reconnection Fee" });
                for (var i = 1; i <= 2; ++i) {
                    var date = i == 1 ? new DateTime(2026, 10, 6) : new DateTime(2026, 11, 8);
                    setup.ReadingSheets.Add(new ReadingSheet { Id = i, ZoneBookId = 1, Status = ReadingStatus.InProgress, BillingDate = date, DueDate = date.AddDays(16) });
                    setup.Readings.Add(new Reading { Id = i, AccountId = 1, ReadingSheetId = i, Status = ReadingStatus.Completed, DateCreated = date.AddDays(-1) });
                    setup.Billings.Add(new Billing { Id = i, AccountId = 1, ReadingId = i, BillingReferenceId = "R" + i,
                        BillingPeriod = date, RemainingAmount = 190, TotalBillAmount = 190, PaymentStatus = (int)PaymentStatus.Unpaid,
                        InvoiceSnapshotJson = "{\"BillAmount\":170,\"MeterMaintenance\":20,\"TotalDue\":190}", ServiceInvoiceNumber = "6J-" + i });
                }
                await setup.SaveChangesAsync(); setup.ChangeTracker.Clear();
            }
            async Task<string?> Disconnect(Guid key, PauseSave? pause = null, int actorId = 1) {
                await using var db = new UserDbContext(Options(pause));
                try { return (await new DisconnectionMonitoringService(db, new BillingService(db)).ConfirmDisconnection("RACE", key, actorId, utc, today)).Id; }
                catch (InvalidOperationException) { return null; }
            }
            async Task<string> Pay(bool firstOnly, PauseSave? pause = null) {
                await using var db = new UserDbContext(Options(pause));
                var billing = new BillingService(db); var request = new CollectionPaymentRequest { Method = PaymentMethod.Cash };
                foreach (var reference in firstOnly ? new[] { "R1" } : new[] { "R1", "R2" }) {
                    var c = (await billing.GetCollectionBill(reference))!;
                    request.Bills.Add(new() { BillingReferenceId = reference, PaidAmount = c.AmountDue,
                        InstallmentIds = c.CollectibleOtherCharges.Select(i => i.InstallmentId).ToList() });
                }
                request.AmountTendered = request.Bills.Sum(b => b.PaidAmount);
                return (await new PaymentService(db).ProcessCollectionPayment(1, request)).First().PaymentReference;
            }
            async Task<string?> Reconnect(Guid key, PauseSave? pause = null, int actorId = 1) {
                await using var db = new UserDbContext(Options(pause));
                try { return (await new DisconnectionMonitoringService(db, new BillingService(db)).ConfirmReconnection("RACE", key, actorId, utc.AddHours(1), today)).Id; }
                catch (InvalidOperationException) { return null; }
            }
            async Task Cancel(string receipt, PauseSave? pause = null) {
                await using var db = new UserDbContext(Options(pause));
                await new PaymentService(db).CancelInvoice(1, new() { InvoiceNo = receipt, Reason = "Synthetic race" });
            }
            async Task WaitForCompetitor(Task competing, PauseSave pause) {
                try { await Task.Delay(150); Require(!competing.IsCompleted, "Competing write bypassed financial locks."); }
                finally { pause.Release.TrySetResult(); }
            }
            foreach (var sameKey in new[] { true, false }) {
                await Seed(); var key = Guid.NewGuid(); var pause = new PauseSave();
                var first = Disconnect(key, pause); await pause.Held.Task.WaitAsync(TimeSpan.FromSeconds(10));
                var second = Disconnect(sameKey ? key : Guid.NewGuid()); await WaitForCompetitor(second, pause);
                var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(20));
                Require(results[0] != null && (sameKey ? results[1] == results[0] : results[1] == null)
                    && await setup.ServiceDisconnectionEvents.CountAsync() == 1 && await setup.OtherCharges.CountAsync() == 1
                    && await setup.OtherChargeInstallments.CountAsync() == 1
                    && (await setup.Accounts.SingleAsync()).Status == (int)AccountStatus.Suspended, "Concurrent confirmations duplicated event/fee/status.");
                Console.WriteLine("PASS: Phase 6J concurrent disconnect " + (sameKey ? "same-key replay" : "distinct-key conflict") + "; one event/350 fee.");
            }
            foreach (var firstActor in new[] { 2, 3 }) {
                await Seed(); var pause = new PauseSave(); var key = Guid.NewGuid();
                var first = Disconnect(key, pause, firstActor); await pause.Held.Task.WaitAsync(TimeSpan.FromSeconds(10));
                var other = Disconnect(key, null, firstActor == 2 ? 3 : 2); await WaitForCompetitor(other, pause);
                var result = await Task.WhenAll(first, other).WaitAsync(TimeSpan.FromSeconds(20)); setup.ChangeTracker.Clear();
                Require(result[0] != null && result[1] == null && await setup.ServiceDisconnectionEvents.CountAsync() == 1
                    && await setup.OtherCharges.CountAsync() == 1 && (await setup.Accounts.SingleAsync()).Status == (int)AccountStatus.Suspended
                    && (await setup.ServiceDisconnectionEvents.SingleAsync()).DisconnectedBy == firstActor, "Cross-role race duplicated event/fee or overwrote audit/status.");
                Console.WriteLine("PASS: Phase 6J cross-role race " + (firstActor == 2 ? "CS OIC first" : "SuperAdmin first") + "; one event/fee, Suspended, original actor preserved.");
            }
            foreach (var paymentFirst in new[] { true, false }) {
                await Seed(); var pause = new PauseSave();
                Task<string?> winner = paymentFirst ? Pay(true, pause).ContinueWith(t => (string?)t.GetAwaiter().GetResult()) : Disconnect(Guid.NewGuid(), pause);
                await pause.Held.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Task<string?> other = paymentFirst ? Disconnect(Guid.NewGuid()) : Pay(true).ContinueWith(t => (string?)t.GetAwaiter().GetResult());
                await WaitForCompetitor(other, pause); var results = await Task.WhenAll(winner, other).WaitAsync(TimeSpan.FromSeconds(20));
                Require((paymentFirst ? results[1] == null : results.All(x => x != null))
                    && await setup.ServiceDisconnectionEvents.CountAsync() == (paymentFirst ? 0 : 1)
                    && await setup.OtherCharges.CountAsync() == (paymentFirst ? 0 : 1)
                    && (await setup.Accounts.SingleAsync()).Status == (int)(paymentFirst ? AccountStatus.Active : AccountStatus.Suspended), "Winning settlement did not protect physical/operational state.");
                Console.WriteLine("PASS: Phase 6J payment/disconnect race " + (paymentFirst ? "payment wins; confirmation rejected" : "confirmation wins; later payment preserves event"));
            }
            foreach (var firstActor in new[] { 2, 3 }) {
                await Seed(); await Disconnect(Guid.NewGuid()); await Pay(false);
                var key = Guid.NewGuid(); var pause = new PauseSave();
                var winner = Reconnect(key, pause, firstActor); await pause.Held.Task.WaitAsync(TimeSpan.FromSeconds(10));
                var other = Reconnect(key, null, firstActor == 2 ? 3 : 2); await WaitForCompetitor(other, pause);
                var result = await Task.WhenAll(winner, other).WaitAsync(TimeSpan.FromSeconds(20)); setup.ChangeTracker.Clear();
                var physical = await setup.ServiceDisconnectionEvents.SingleAsync();
                Require(result[0] != null && result[1] == null && physical.ReconnectedBy == firstActor
                    && physical.ReconnectionOperationId == key.ToString("D") && physical.ActualReconnectionDateUtc != null
                    && (await setup.Accounts.SingleAsync()).Status == (int)AccountStatus.Active
                    && await setup.OtherCharges.CountAsync() == 1 && await setup.OtherChargeInstallments.CountAsync() == 1,
                    "Cross-role reconnection race overwrote audit, duplicated fee or lost Active status.");
                Console.WriteLine("PASS: Phase 6J-F cross-role reconnection race " + (firstActor == 2 ? "CS OIC first" : "SuperAdmin first") + "; original actor, one reconnection, Active, no new fee.");
            }
            foreach (var cancellationFirst in new[] { true, false }) {
                await Seed(); await Disconnect(Guid.NewGuid()); var receipt = await Pay(false); var pause = new PauseSave();
                Task winner = cancellationFirst ? Cancel(receipt, pause) : Reconnect(Guid.NewGuid(), pause);
                await pause.Held.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Task other = cancellationFirst ? Reconnect(Guid.NewGuid()) : Cancel(receipt);
                await WaitForCompetitor(other, pause); await Task.WhenAll(winner, other).WaitAsync(TimeSpan.FromSeconds(20));
                setup.ChangeTracker.Clear(); var state = await new DisconnectionMonitoringService(setup, new BillingService(setup)).Get("RACE", today);
                Require(state?.Status == (cancellationFirst ? "BALANCE + RECONNECTION FEE PENDING" : "FINANCIAL / SERVICE STATE REVIEW REQUIRED")
                    && state.ActualReconnectionDateUtc.HasValue == !cancellationFirst
                    && (await setup.Accounts.SingleAsync()).Status == (int)(cancellationFirst ? AccountStatus.Suspended : AccountStatus.Active), "Reconnection/cancellation race silently reversed service state.");
                Console.WriteLine("PASS: Phase 6J reconnection/cancellation race " + (cancellationFirst ? "cancellation wins; reconnection rejected" : "reconnection wins; manual review flagged"));
            }
        }
        finally {
            foreach (var table in created.AsEnumerable().Reverse()) await setup.Database.ExecuteSqlRawAsync("DROP TABLE `" + table + "`");
        }
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    sealed class PauseSave : SaveChangesInterceptor
    {
        public TaskCompletionSource Held { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken cancellationToken = default) {
            if (Held.TrySetResult()) await Release.Task.WaitAsync(TimeSpan.FromSeconds(10));
            return result;
        }
    }
}
