using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

static class InvoiceConcurrency
{
    public static async Task Run(MySqlConnectionStringBuilder settings, string migration)
    {
        // Never use operational tables: concurrent connections need a shared disposable schema.
        var prefix = "tm_si_" + Guid.NewGuid().ToString("N") + "_";
        var rename = new SyntheticInvoiceTables(prefix);
        var adminSettings = new MySqlConnectionStringBuilder(settings.ConnectionString);
        await using var admin = new MySqlConnection(adminSettings.ConnectionString);
        await admin.OpenAsync();
        var cleanupAllowed = false;
        await using (var grants = new MySqlCommand("SHOW GRANTS FOR CURRENT_USER", admin))
        await using (var reader = await grants.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var grant = reader.GetString(0);
                if ((grant.Contains(" ON *.* ") || grant.Contains($" ON `{settings.Database}`.* "))
                    && System.Text.RegularExpressions.Regex.IsMatch(grant, @"\b(DROP|ALL PRIVILEGES)\b"))
                    cleanupAllowed = true;
            }
        }
        if (!cleanupAllowed)
        {
            await using var leftovers = new MySqlCommand("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name REGEXP '^tm_si_[a-f0-9]{32}_(billings|bir_invoice_configs|service_invoice_seq)$'", admin);
            var count = Convert.ToInt32(await leftovers.ExecuteScalarAsync());
            throw new InvalidOperationException($"Real concurrency/migration regression requires a LOCAL BILLING_TEST_CONNECTION with CREATE/ALTER/DROP privileges. No new tables were created in this run. Earlier synthetic test tables awaiting cleanup: {count}.");
        }
        // Serialize harness runs so cleanup cannot drop another active test's tables.
        await using (var testLock = new MySqlCommand("SELECT GET_LOCK(@name, 10)", admin))
        {
            testLock.Parameters.AddWithValue("@name", "tmcwd_invoice_regression_" + settings.Database);
            if (Convert.ToInt32(await testLock.ExecuteScalarAsync()) != 1) throw new Exception("Another invoice regression is running.");
        } // MySQL releases this named lock when the admin connection closes.
        // Clean only this harness's uniquely named tables left by interrupted runs.
        var oldTables = new List<string>();
        await using (var leftovers = new MySqlCommand("SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name REGEXP '^tm_si_[a-f0-9]{32}_(billings|bir_invoice_configs|service_invoice_seq)$'", admin))
        await using (var reader = await leftovers.ExecuteReaderAsync())
            while (await reader.ReadAsync()) oldTables.Add(reader.GetString(0));
        foreach (var table in oldTables)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(table, @"^tm_si_[a-f0-9]{32}_(billings|bir_invoice_configs|service_invoice_seq)$"))
                throw new Exception("Unsafe synthetic table cleanup target.");
            await new MySqlCommand($"DROP TABLE `{table}`", admin).ExecuteNonQueryAsync();
        }
        var created = false;
        try
        {
            var isolated = new MySqlConnectionStringBuilder(settings.ConnectionString) { Pooling = false };
            var options = new DbContextOptionsBuilder<UserDbContext>().UseMySql(isolated.ConnectionString,
                ServerVersion.AutoDetect(admin)).AddInterceptors(rename).Options;
            await using var setup = new UserDbContext(options);
            // Only the Billing table is needed before the additive migration.
            var ddl = System.Text.RegularExpressions.Regex.Match(setup.Database.GenerateCreateScript(),
                @"CREATE TABLE `Billings`[\s\S]*?;").Value;
            if (ddl.Length == 0) throw new Exception("Missing test Billing schema.");
            await setup.Database.ExecuteSqlRawAsync(ddl);
            created = true;
            setup.Billings.Add(new Billing { AccountId = 1, BillingReferenceId = "HISTORICAL",
                BillingPeriod = DateTime.Now, DateCreated = DateTime.Now, DateUpdated = DateTime.Now,
                PaymentStatus = (int)PaymentStatus.Unpaid });
            await setup.SaveChangesAsync();
            setup.ChangeTracker.Clear();
            await setup.Database.ExecuteSqlRawAsync("ALTER TABLE `Billings` DROP COLUMN `ServiceInvoiceNumber`");
            await setup.Database.ExecuteSqlRawAsync("ALTER TABLE `Billings` DROP COLUMN `InvoiceSnapshotJson`");
            var script = await File.ReadAllTextAsync(migration);
            await setup.Database.ExecuteSqlRawAsync(script);
            await setup.Database.ExecuteSqlRawAsync(script); // Safe rerun.
            var snapshotMigration = await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(migration)!, "20261006_add_official_invoice_snapshot.sql"));
            await setup.Database.ExecuteSqlRawAsync(snapshotMigration);
            await setup.Database.ExecuteSqlRawAsync(snapshotMigration);
            var historical = await setup.Billings.AsNoTracking().SingleAsync();
            if (historical.BillingReferenceId != "HISTORICAL" || historical.ServiceInvoiceNumber != null || historical.InvoiceSnapshotJson != null)
                throw new Exception("Invoice migration changed or backfilled a historical billing row.");
            setup.BirInvoiceConfigs.Add(new() { Id = 1, PTUNumber = "CONCURRENT-SYNTHETIC", PermitDateIssued = new DateOnly(2026, 1, 1),
                SeriesFrom = 77, SeriesTo = 78, SeriesWidth = 5, InvoicePrefix = "CON-", IsActive = true,
                DateCreated = DateTime.Now, DateUpdated = DateTime.Now });
            await setup.SaveChangesAsync();
            var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<string> Allocate(bool first)
            {
                await using var db = new UserDbContext(options);
                await using var tx = await db.Database.BeginTransactionAsync();
                var billing = new Billing { AccountId = 1, BillingReferenceId = first ? "INTERNAL-A" : "INTERNAL-B",
                    BillingPeriod = DateTime.Now, DateCreated = DateTime.Now, DateUpdated = DateTime.Now,
                    PaymentStatus = (int)PaymentStatus.Unpaid };
                if (!first) entered.SetResult();
                var number = await new BillingService(db).AssignServiceInvoiceNumber(billing);
                if (first) { held.SetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
                db.Billings.Add(billing); await db.SaveChangesAsync(); await tx.CommitAsync(); return number;
            }
            var first = Allocate(true);
            await held.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var second = Allocate(false);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            try
            {
                await Task.Delay(200);
                if (second.IsCompleted) throw new Exception("Concurrent allocation did not wait for the config row lock.");
            }
            finally { release.TrySetResult(); }
            var numbers = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(15));
            if (!numbers.SequenceEqual(new[] { "CON-00077", "CON-00078" })
                || await setup.Billings.CountAsync() != 3
                || (await setup.ServiceInvoiceSeq.AsNoTracking().SingleAsync()).LastValue != 78)
                throw new Exception("Concurrent allocation duplicated, skipped or lost a serial.");
            Console.WriteLine("PASS: Phase 6D real two-connection row locking, unique first/last serials and additive migration rerun (synthetic local tables).");
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Synthetic invoice regression failed before cleanup: {error.GetType().Name}: {error.Message}");
            throw;
        }
        finally
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(prefix, @"^tm_si_[a-f0-9]{32}_$"))
                throw new Exception("Unsafe regression cleanup target.");
            if (created)
                foreach (var table in SyntheticInvoiceTables.Names)
                    await new MySqlCommand($"DROP TABLE IF EXISTS `{prefix}{table}`", admin).ExecuteNonQueryAsync();
        }
    }
}

// Rewrite only test SQL table names; actual services and database transactions are unchanged.
sealed class SyntheticInvoiceTables(string prefix, string[]? tables = null) : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
{
    public static readonly string[] Names = ["Billings", "bir_invoice_configs", "service_invoice_seq"];
    void Rewrite(System.Data.Common.DbCommand command)
    {
        foreach (var name in tables ?? Names) command.CommandText = System.Text.RegularExpressions.Regex.Replace(
            command.CommandText, @"\b" + name + @"\b", prefix + name);
    }
    public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> NonQueryExecutingAsync(
        System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
        Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result, CancellationToken cancellationToken = default)
    { Rewrite(command); return ValueTask.FromResult(result); }
    public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
        System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
        Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
    { Rewrite(command); return ValueTask.FromResult(result); }
}
