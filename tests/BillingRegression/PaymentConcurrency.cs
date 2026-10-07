using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MySqlConnector;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

static class PaymentConcurrency
{
    public static async Task Run(MySqlConnectionStringBuilder settings)
    {
        if (settings.Database != "tmcwd_test" || !new[] { "localhost", "127.0.0.1", "::1" }.Contains(settings.Server))
            throw new Exception("Payment regressions require the dedicated local tmcwd_test database.");
        var prefix = "tm_pay_" + Guid.NewGuid().ToString("N")[..16] + "_";
        string[] tables = ["Billings", "payments", "penalties", "other_charges", "water_connection_applications", "billing_adjustments", "senior_citizen_discounts"];
        var isolated = new MySqlConnectionStringBuilder(settings.ConnectionString) { Pooling = false };
        var rename = new SyntheticInvoiceTables(prefix, tables);
        var options = new DbContextOptionsBuilder<UserDbContext>().UseMySql(isolated.ConnectionString,
            new MySqlServerVersion(new Version(8, 0, 0))).AddInterceptors(rename).Options;
        await using var setup = new UserDbContext(options);
        var created = new List<string>();
        try {
            foreach (var table in tables) {
                var ddl = System.Text.RegularExpressions.Regex.Match(setup.Database.GenerateCreateScript(),
                    $@"CREATE TABLE `{table}`[\s\S]*?;").Value;
                if (ddl.Length == 0) throw new Exception("Missing synthetic payment schema.");
                await setup.Database.ExecuteSqlRawAsync(ddl);
                created.Add(table);
            }
            setup.Billings.AddRange(new[] { "A", "B" }.Select(reference => new Billing {
                AccountId = 1, BillingReferenceId = reference, TotalBillAmount = 100m, RemainingAmount = 100m,
                PaymentStatus = (int)PaymentStatus.Unpaid, BillingPeriod = DateTime.Today,
                DateCreated = DateTime.Now, DateUpdated = DateTime.Now }));
            await setup.SaveChangesAsync(); setup.ChangeTracker.Clear();

            async Task Race(string firstBill, string secondBill, string firstReference, string secondReference)
            {
                var pause = new PausePayment();
                async Task<bool> Pay(string bill, string reference, bool hold) {
                    var builder = new DbContextOptionsBuilder<UserDbContext>().UseMySql(isolated.ConnectionString,
                        new MySqlServerVersion(new Version(8, 0, 0))).AddInterceptors(rename);
                    if (hold) builder.AddInterceptors(pause);
                    await using var db = new UserDbContext(builder.Options);
                    try {
                        await new PaymentService(db).ProcessCollectionPayment(7, new() {
                            InvoiceNumber = reference, Method = PaymentMethod.Cash, AmountTendered = 100m,
                            Bills = new() { new() { BillingReferenceId = bill, PaidAmount = 100m } } });
                        return true;
                    }
                    catch (InvalidOperationException) { return false; }
                }
                var first = Pay(firstBill, firstReference, true);
                await pause.Held.Task.WaitAsync(TimeSpan.FromSeconds(10));
                var second = Pay(secondBill, secondReference, false);
                try {
                    await Task.Delay(200);
                    if (second.IsCompleted) throw new Exception("Concurrent payment did not wait for financial locks.");
                }
                finally { pause.Release.TrySetResult(); }
                var result = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(20));
                if (!result.SequenceEqual(new[] { true, false })) throw new Exception("Concurrent payment was duplicated or lost.");
            }
            await Race("A", "B", "SHARED-REFERENCE", "SHARED-REFERENCE");
            if (await setup.Payments.CountAsync() != 1 || (await setup.Billings.AsNoTracking().SingleAsync(b => b.BillingReferenceId == "B")).RemainingAmount != 100m)
                throw new Exception("Same-reference race changed another bill.");
            await Race("B", "B", "SECOND-REFERENCE", "THIRD-REFERENCE");
            if (await setup.Payments.CountAsync() != 2 || await setup.Billings.AnyAsync(b => b.RemainingAmount != 0m))
                throw new Exception("Same-bill race created overpayment or an inconsistent balance.");
            Console.WriteLine("PASS: two-connection payment reference uniqueness across bills and same-bill concurrent settlement.");
        }
        finally {
            if (!System.Text.RegularExpressions.Regex.IsMatch(prefix, @"^tm_pay_[a-f0-9]{16}_$"))
                throw new Exception("Unsafe synthetic payment cleanup target.");
            foreach (var table in created.AsEnumerable().Reverse())
                await setup.Database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS `{table}`");
        }
    }
}

sealed class PausePayment : SaveChangesInterceptor
{
    public TaskCompletionSource Held { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (data.Context!.ChangeTracker.Entries<Payment>().Any(e => e.State == EntityState.Added)) {
            Held.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        return result;
    }
}
