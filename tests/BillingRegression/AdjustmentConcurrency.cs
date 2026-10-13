using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MySqlConnector;
using System.Text.RegularExpressions;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

static class AdjustmentConcurrency
{
    public static async Task Run(MySqlConnectionStringBuilder settings, string root)
    {
        if (settings.Database != "tmcwd_test" || !new[] { "localhost", "127.0.0.1", "::1" }.Contains(settings.Server))
            throw new Exception("Adjustment concurrency requires local tmcwd_test.");
        var prefix = "tm_adj_" + Guid.NewGuid().ToString("N")[..16] + "_";
        string[] tables = ["Billings", "billing_adjustments", "billing_adjustment_operations", "users", "payments",
            "payment_receipts", "penalties", "other_charges", "other_charge_installments", "accounts", "customers",
            "water_connection_applications", "senior_citizen_discounts", "invoice_cancellations", "collection_remittances"];
        var isolated = new MySqlConnectionStringBuilder(settings.ConnectionString) { Pooling = false };
        var rename = new SyntheticInvoiceTables(prefix, tables);
        DbContextOptions<UserDbContext> Options(PauseSave? pause = null) {
            var options = new DbContextOptionsBuilder<UserDbContext>().UseMySql(isolated.ConnectionString,
                new MySqlServerVersion(new Version(8, 0, 46))).AddInterceptors(rename);
            if (pause != null) options.AddInterceptors(pause);
            return options.Options;
        }
        await using var setup = new UserDbContext(Options()); var created = new List<string>();
        try {
            foreach (var table in tables.Where(t => t != "billing_adjustment_operations")) {
                var ddl = Regex.Match(setup.Database.GenerateCreateScript(), $@"CREATE TABLE `{table}`[\s\S]*?;").Value;
                Require(ddl.Length > 0, "Missing synthetic concurrency schema.");
                await setup.Database.ExecuteSqlRawAsync(ddl); created.Add(table);
            }
            var migration = await File.ReadAllTextAsync(Path.Combine(root,
                "TMCWD/TMCWD.Data/Database/20261012_add_billing_adjustment_operations.sql"));
            await setup.Database.ExecuteSqlRawAsync(migration); created.Add("billing_adjustment_operations");
            await setup.Database.ExecuteSqlRawAsync(migration);
            var snapshot = "{\"immutable\":true}";
            setup.Users.Add(new User { Id = 1, Role = 7, IsActive = true, IsVerified = true });
            setup.Billings.AddRange(new[] { "SAME", "DISTINCT", "ADJUST-FIRST", "PAY-FIRST" }.Select((reference, i) => new Billing {
                BillingReferenceId = reference, AccountId = i + 1, TotalBillAmount = 120, RemainingAmount = 120,
                PaymentStatus = (int)PaymentStatus.Unpaid, ServiceInvoiceNumber = "C1-" + i, InvoiceSnapshotJson = snapshot,
                BillingPeriod = DateTime.Today, DateCreated = DateTime.Now, DateUpdated = DateTime.Now }));
            await setup.SaveChangesAsync(); setup.ChangeTracker.Clear();
            async Task<long?> Adjust(string reference, Guid key, PauseSave? pause = null) {
                await using var db = new UserDbContext(Options(pause));
                try { return (await new BillingAdjustmentService(db).SaveBatch(1,
                    [new() { BillingReferenceId = reference, Amount = 30, Type = 2 }], key)).Single().Id; }
                catch (InvalidOperationException) { return null; }
            }
            async Task<string?> Pay(string reference, PauseSave? pause = null) {
                await using var db = new UserDbContext(Options(pause));
                try { return (await new PaymentService(db).ProcessCollectionPayment(1, new() {
                    Method = PaymentMethod.Cash, AmountTendered = 120,
                    Bills = [new() { BillingReferenceId = reference, PaidAmount = 120 }] })).Single().PaymentReference; }
                catch (TMCWD.Model.Billing.PaymentConflictException) { return null; }
            }
            foreach (var sameKey in new[] { true, false }) {
                var reference = sameKey ? "SAME" : "DISTINCT"; var key = Guid.NewGuid(); var pause = new PauseSave();
                var first = Adjust(reference, key, pause);
                await pause.Held.Task.WaitAsync(TimeSpan.FromSeconds(10));
                var second = Adjust(reference, sameKey ? key : Guid.NewGuid());
                try { await Task.Delay(100); Require(!second.IsCompleted, "Concurrent adjustment bypassed billing lock."); }
                finally { pause.Release.TrySetResult(); }
                var result = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(20));
                setup.ChangeTracker.Clear(); var bill = await setup.Billings.SingleAsync(b => b.BillingReferenceId == reference);
                Require(result.All(id => id.HasValue) && (sameKey ? result[0] == result[1] : result[0] != result[1])
                    && bill.RemainingAmount == (sameKey ? 90 : 60)
                    && await setup.BillingAdjustmentOperations.CountAsync(o => o.BillingReferenceId == reference) == (sameKey ? 1 : 2),
                    "Concurrent idempotency or accumulated balance failed.");
                Console.WriteLine("PASS: adjustment concurrency " + (sameKey ? "same key applies once" : "distinct keys serialize without lost update"));
            }
            foreach (var adjustmentFirst in new[] { true, false }) {
                var reference = adjustmentFirst ? "ADJUST-FIRST" : "PAY-FIRST"; var pause = new PauseSave();
                Task<long?> adjustment; Task<string?> payment;
                if (adjustmentFirst) {
                    adjustment = Adjust(reference, Guid.NewGuid(), pause);
                    await pause.Held.Task.WaitAsync(TimeSpan.FromSeconds(10)); payment = Pay(reference);
                } else {
                    payment = Pay(reference, pause);
                    await pause.Held.Task.WaitAsync(TimeSpan.FromSeconds(10)); adjustment = Adjust(reference, Guid.NewGuid());
                }
                try { await Task.Delay(100); Require(adjustmentFirst ? !payment.IsCompleted : !adjustment.IsCompleted,
                    "Payment/adjustment bypassed shared billing lock."); }
                finally { pause.Release.TrySetResult(); }
                await Task.WhenAll(adjustment, payment).WaitAsync(TimeSpan.FromSeconds(20));
                setup.ChangeTracker.Clear(); var bill = await setup.Billings.SingleAsync(b => b.BillingReferenceId == reference);
                Require(adjustmentFirst ? adjustment.Result.HasValue && payment.Result == null && bill.RemainingAmount == 90
                    : adjustment.Result == null && payment.Result != null && bill.RemainingAmount == 0,
                    "Payment/adjustment ordering corrupted balance.");
                Require(bill.InvoiceSnapshotJson == snapshot && bill.ServiceInvoiceNumber == (adjustmentFirst ? "C1-2" : "C1-3")
                    && await setup.Payments.CountAsync(p => p.BillingReference == reference) == (adjustmentFirst ? 0 : 1)
                    && await setup.BillingAdjustments.CountAsync(a => a.BillingReferenceId == reference) == (adjustmentFirst ? 1 : 0),
                    "Race changed snapshot/identity or partially committed.");
                Console.WriteLine("PASS: payment/adjustment race " + (adjustmentFirst ? "adjustment wins; stale payment rejected" : "payment wins; settled adjustment rejected"));
            }
            Console.WriteLine("PASS: additive adjustment operation migration rerun; four real two-connection concurrency cases.");
        }
        finally {
            foreach (var table in created.AsEnumerable().Reverse())
                await setup.Database.ExecuteSqlRawAsync("DROP TABLE `" + table + "`");
        }
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    sealed class PauseSave : SaveChangesInterceptor
    {
        public TaskCompletionSource Held { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (Held.TrySetResult()) await Release.Task.WaitAsync(TimeSpan.FromSeconds(10));
            return result;
        }
    }
}
