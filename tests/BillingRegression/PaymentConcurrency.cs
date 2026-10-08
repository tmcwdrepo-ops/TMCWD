using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MySqlConnector;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;
using PaymentConflictException = TMCWD.Model.Billing.PaymentConflictException;
using System.Text.RegularExpressions;

static class PaymentConcurrency
{
    public static async Task Run(MySqlConnectionStringBuilder settings, string root)
    {
        if (settings.Database != "tmcwd_test" || !new[] { "localhost", "127.0.0.1", "::1" }.Contains(settings.Server))
            throw new Exception("Payment regressions require dedicated local tmcwd_test.");
        var prefix = "tm_pay_" + Guid.NewGuid().ToString("N")[..16] + "_";
        string[] tables = ["Billings", "payments", "payment_receipts", "penalties", "other_charges", "other_charge_installments", "water_connection_applications", "billing_adjustments", "senior_citizen_discounts", "invoice_cancellations", "users", "accounts", "customers", "collection_remittances"];
        var isolated = new MySqlConnectionStringBuilder(settings.ConnectionString) { Pooling = false, AllowUserVariables = true };
        var rename = new SyntheticInvoiceTables(prefix, tables);
        DbContextOptions<UserDbContext> Options(SaveChangesInterceptor? pause = null) {
            var builder = new DbContextOptionsBuilder<UserDbContext>().UseMySql(isolated.ConnectionString, new MySqlServerVersion(new Version(8, 0, 0))).AddInterceptors(rename);
            if (pause != null) builder.AddInterceptors(pause);
            return builder.Options;
        }
        await using var setup = new UserDbContext(Options());
        var created = new List<string>();
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        try {
            await setup.Database.OpenConnectionAsync();
            foreach (var table in tables) {
                var ddl = Regex.Match(setup.Database.GenerateCreateScript(), $@"CREATE TABLE `{table}`[\s\S]*?;").Value;
                Check(ddl.Length > 0, "Missing synthetic payment schema: " + table);
                await setup.Database.ExecuteSqlRawAsync(ddl); created.Add(table);
            }
            setup.Payments.AddRange(new[] { "OLD-A", "OLD-B" }.Select(reference => new Payment {
                BillingReference = reference, PaymentReference = "CR000125", CreatedBy = 7, Method = 1,
                PaidAmount = 50m, Success = true, DateCreated = DateTime.Today.AddDays(-1), MacAddress = "TEST" }));
            await setup.SaveChangesAsync(); setup.ChangeTracker.Clear();
            await setup.Database.ExecuteSqlRawAsync("DROP TABLE payment_receipts");
            await setup.Database.ExecuteSqlRawAsync("ALTER TABLE payments DROP COLUMN BalanceBefore, DROP COLUMN ChargesOnly");
            await setup.Database.ExecuteSqlRawAsync("ALTER TABLE penalties DROP COLUMN SettledPaymentId");
            await setup.Database.ExecuteSqlRawAsync("ALTER TABLE other_charges DROP COLUMN SettledPaymentId");
            var migration = Regex.Replace(await File.ReadAllTextAsync(Path.Combine(root, "TMCWD/TMCWD.Data/Database/20261007_add_collection_receipts.sql")), @"(?m)^--.*$", "");
            for (var run = 0; run < 2; run++)
                foreach (var sql in migration.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) await setup.Database.ExecuteSqlRawAsync(sql.Replace("{", "{{").Replace("}", "}}"));
            Check(await setup.PaymentReceipts.CountAsync() == 1 && await setup.Payments.CountAsync() == 2, "Migration lost bill lines or duplicated legacy receipt.");
            Console.WriteLine("PASS: additive receipt migration rerun preserves legacy multi-bill rows and seeds shared sequence.");
            setup.Users.AddRange(new User { Id = 7, Name = "Cashier One", Role = (int)UserRole.Cashier }, new User { Id = 12, Name = "Cashier Two", Role = (int)UserRole.Cashier });
            setup.Billings.AddRange(new[] { "A", "B", "C", "D", "E", "F" }.Select((reference, index) => new Billing {
                AccountId = index + 1, BillingReferenceId = reference, TotalBillAmount = 190m, RemainingAmount = 190m,
                ServiceInvoiceNumber = "000000" + (index + 1), InvoiceSnapshotJson = "{\"immutable\":true}",
                PaymentStatus = (int)PaymentStatus.Unpaid, BillingPeriod = DateTime.Today, DateCreated = DateTime.Now, DateUpdated = DateTime.Now }));
            await setup.SaveChangesAsync(); setup.ChangeTracker.Clear();
            async Task<string?> Pay(string bill, int cashier, PaymentMethod method, PausePayment? pause = null, decimal amount = 190m) {
                await using var db = new UserDbContext(Options(pause));
                try {
                    var payments = await new PaymentService(db).ProcessCollectionPayment(cashier, new() {
                        InvoiceNumber = "UNTRUSTED-SAME-CLIENT-REFERENCE", ExternalReference = "EXTERNAL-" + cashier,
                        Method = method, AmountTendered = amount,
                        Bills = new() { new() { BillingReferenceId = bill, PaidAmount = amount } } });
                    return payments.Single().PaymentReference;
                } catch (PaymentConflictException error) {
                    Check(error.Message == "The bill balance changed or has already been paid. Refresh and try again.", "Conflict message changed."); return null;
                }
            }
            async Task<string[]> Race(string firstBill, string secondBill, PaymentMethod method) {
                var pause = new PausePayment();
                var first = Pay(firstBill, 7, method, pause);
                await pause.Held.Task.WaitAsync(TimeSpan.FromSeconds(10));
                var second = Pay(secondBill, 12, method);
                try {
                    if (firstBill == secondBill) { await Task.Delay(200); Check(!second.IsCompleted, "Second cashier bypassed same-bill lock."); }
                    else Check(await second.WaitAsync(TimeSpan.FromSeconds(10)) != null, "Unrelated bill did not succeed independently.");
                } finally { pause.Release.TrySetResult(); }
                var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(20));
                Check(results[0] != null && (firstBill == secondBill ? results[1] == null : results[1] != null && results[0] != results[1]), "Race duplicated or lost payment.");
                return results.Where(r => r != null).Select(r => r!).ToArray();
            }
            var cashRefs = await Race("A", "B", PaymentMethod.Cash);
            Check(cashRefs.SequenceEqual(new[] { "CR000126", "CR000127" }), "Shared sequence did not continue legacy numbers.");
            await Race("C", "C", PaymentMethod.Check);
            await Race("D", "E", PaymentMethod.EWallet);
            Check(await Pay("C", 12, PaymentMethod.Cash) == null, "Stale paid bill accepted.");
            Check(await setup.Payments.CountAsync(p => p.BillingReference == "C") == 1 && !await setup.Billings.AnyAsync(b => b.RemainingAmount < 0m), "Double payment or negative balance.");
            var service = new PaymentService(setup);
            var one = await service.GetByDateRange(DateTime.Today, DateTime.Today, 7);
            var two = await service.GetByDateRange(DateTime.Today, DateTime.Today, 12);
            Check(one.Count == 3 && one.All(p => p.CreatedBy == 7) && two.Count == 2 && two.All(p => p.CreatedBy == 12) && (await service.GetByDateRange(DateTime.Today, DateTime.Today)).Count == 5, "Cashier/supervisor daily totals mixed ownership.");
            Check(one.Where(p => p.Method == 1).Sum(p => p.PaidAmount) == 190m && two.Where(p => p.Method == 1).Sum(p => p.PaidAmount) == 190m, "Cash includes check/online.");
            foreach (var cashier in new[] { 7, 12 }) {
                await using var remittanceDb = new UserDbContext(Options());
                var controller = new TMCWD.Data.Controllers.CollectionRemittanceController(remittanceDb) {
                    ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext {
                        User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[] {
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, cashier.ToString()),
                            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, ((int)UserRole.Cashier).ToString()) }, "test")) } } };
                var result = await controller.Save(new() { CollectorId = 999, Date = DateTime.Today, CashOnHand = 999m, Counts = new() { ["100"] = 1m } });
                Check(result is Microsoft.AspNetCore.Mvc.OkObjectResult, "Own-cashier remittance failed.");
            }
            Check(await setup.CollectionRemittances.CountAsync() == 2 && await setup.CollectionRemittances.AllAsync(r => (r.CollectorId == 7 || r.CollectorId == 12) && r.CashOnHand == 190m && r.CountedCash == 100m), "Remittance trusted browser identity/totals or mixed cashiers.");
            Check((await service.GetByDateRange(DateTime.Today, DateTime.Today, 7)).Where(p => p.Method == 1).Sum(p => p.PaidAmount) == 190m, "Remittance invented a cash deduction.");
            Check(await setup.PaymentReceipts.Where(r => r.Method != 1).AllAsync(r => r.ExternalReference != null && r.ExternalReference != r.PaymentReference), "External reference replaced receipt identity.");
            await using (var duplicate = new UserDbContext(Options())) {
                duplicate.PaymentReceipts.Add(new PaymentReceipt { PaymentReference = cashRefs[0], CreatedBy = 12, Method = 1, DateCreated = DateTime.Now });
                var rejected = false;
                try { await duplicate.SaveChangesAsync(); } catch (DbUpdateException e) when (e.InnerException is MySqlException { Number: 1062 }) { rejected = true; }
                Check(rejected, "Receipt database unique constraint missing.");
            }
            var headerCount = await setup.PaymentReceipts.CountAsync();
            var paymentCount = await setup.Payments.CountAsync();
            var balancesBeforeReprint = await setup.Billings.AsNoTracking().OrderBy(b => b.Id).Select(b => b.RemainingAmount).ToListAsync();
            var invoice = await service.GetInvoice(cashRefs[0]); await service.SearchInvoices("CR");
            Check(invoice?.CashierId == 7 && invoice.CashierName == "Cashier One" && invoice.TotalAmount == 190m && invoice.TransactionType == "Cash" && await setup.PaymentReceipts.CountAsync() == headerCount && await setup.Payments.CountAsync() == paymentCount, "Reprint/search mutated receipts or cashier identity.");
            Check((await setup.Billings.AsNoTracking().OrderBy(b => b.Id).Select(b => b.RemainingAmount).ToListAsync()).SequenceEqual(balancesBeforeReprint)
                && (await service.GetByDateRange(DateTime.Today, DateTime.Today, 7)).Sum(p => p.PaidAmount) == one.Sum(p => p.PaidAmount), "Reprint mutated balance or cashier totals.");
            await using (var other = new UserDbContext(Options())) {
                var denied = false;
                try { await new PaymentService(other).CancelInvoice(12, new() { InvoiceNo = cashRefs[0], Reason = "Wrong cashier" }); } catch (UnauthorizedAccessException) { denied = true; }
                Check(denied, "Cashier cancelled another cashier's receipt.");
            }
            await using (var cancel = new UserDbContext(Options())) await new PaymentService(cancel).CancelInvoice(7, new() { InvoiceNo = cashRefs[0], Reason = "Synthetic cancellation" });
            Check(!await setup.Payments.AnyAsync(p => p.PaymentReference == cashRefs[0] && p.Success) && await setup.Payments.AnyAsync(p => p.PaymentReference == cashRefs[1] && p.Success), "Cancellation changed other cashier receipt.");
            Check((await setup.Billings.AsNoTracking().SingleAsync(b => b.BillingReferenceId == "A")).RemainingAmount == 190m, "Cancellation did not restore exact balance.");
            await using (var repeated = new UserDbContext(Options())) {
                var rejected = false;
                try { await new PaymentService(repeated).CancelInvoice(7, new() { InvoiceNo = cashRefs[0], Reason = "Repeated cancellation" }); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected && await setup.InvoiceCancellations.CountAsync(c => c.PaymentReference == cashRefs[0]) == 1, "Cancellation restored balance or audit twice.");
            }
            Check((await service.GetByDateRange(DateTime.Today, DateTime.Today, 7)).Where(p => p.Method == 1).Sum(p => p.PaidAmount) == 0m, "Cancellation counted as cash.");
            var replacement = await Pay("A", 7, PaymentMethod.Cash);
            Check(replacement != null && !cashRefs.Contains(replacement), "Cancelled reference reused.");
            var cancelPause = new PausePayment(true);
            async Task CancelWhileHeld() {
                await using var cancelDb = new UserDbContext(Options(cancelPause));
                await new PaymentService(cancelDb).CancelInvoice(7, new() { InvoiceNo = replacement!, Reason = "Concurrent correction" });
            }
            var cancelTask = CancelWhileHeld();
            await cancelPause.Held.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var retryTask = Pay("A", 12, PaymentMethod.Cash);
            try { await Task.Delay(200); Check(!retryTask.IsCompleted, "Payment bypassed cancellation's bill lock."); }
            finally { cancelPause.Release.TrySetResult(); }
            await cancelTask.WaitAsync(TimeSpan.FromSeconds(10));
            Check(await retryTask.WaitAsync(TimeSpan.FromSeconds(10)) != null, "Payment after committed cancellation was lost/deadlocked.");
            var feeBill = await setup.Billings.SingleAsync(b => b.BillingReferenceId == "F");
            Check(feeBill.RemainingAmount == 190m, "Water balance changed before the fee.");
            var fee = new OtherCharge { BillingReferenceId = "F", Amount = 20m, PayableIn = "1", InstallmentCount = 1, PaymentStatus = (int)PaymentStatus.Unpaid,
                IsActive = true, DateCreated = DateTime.Now, DateUpdated = DateTime.Now };
            setup.OtherCharges.Add(fee); await setup.SaveChangesAsync(); setup.ChangeTracker.Clear();
            setup.OtherChargeInstallments.Add(new OtherChargeInstallment { OtherChargeId = fee.Id, InstallmentNumber = 1,
                Amount = 20m, DueBillingPeriod = DateTime.Today, PaymentStatus = (int)PaymentStatus.Unpaid });
            await setup.SaveChangesAsync();
            var feeId = (await setup.OtherChargeInstallments.SingleAsync()).Id;
            var feePause = new PausePayment();
            async Task<string> CollectFee() {
                await using var feeDb = new UserDbContext(Options(feePause));
                return await new OtherChargeService(feeDb).ProcessPayment(7, new() { Method = PaymentMethod.Check,
                    InstallmentIds = new() { feeId }, AmountTendered = 20m, ExternalReference = "CHECK-FEE" });
            }
            var feeTask = CollectFee();
            await feePause.Held.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var staleFull = Pay("F", 12, PaymentMethod.Cash, amount: 210m);
            try { await Task.Delay(200); Check(!staleFull.IsCompleted, "Collection bypassed Other Charges bill lock."); }
            finally { feePause.Release.TrySetResult(); }
            var feeReference = await feeTask.WaitAsync(TimeSpan.FromSeconds(10));
            Check(await staleFull.WaitAsync(TimeSpan.FromSeconds(10)) == null && await Pay("F", 12, PaymentMethod.Cash) != null,
                "Charge collection accepted stale full-balance payment or lost remainder.");
            Check(await setup.Payments.Where(p => p.BillingReference == "F").SumAsync(p => p.PaidAmount) == 210m
                && await setup.Payments.AnyAsync(p => p.PaymentReference == feeReference && p.ChargesOnly), "Charge/full-bill race duplicated settlement.");
            var finalBills = await setup.Billings.AsNoTracking().OrderBy(b => b.BillingReferenceId).ToListAsync();
            Check(finalBills.Select(b => b.BillingReferenceId).SequenceEqual(new[] { "A", "B", "C", "D", "E", "F" })
                && finalBills.Select(b => b.ServiceInvoiceNumber).SequenceEqual(Enumerable.Range(1, 6).Select(i => "000000" + i))
                && finalBills.All(b => b.InvoiceSnapshotJson == "{\"immutable\":true}"), "Payment changed invoice identity, internal billing reference or snapshot.");
            Console.WriteLine("PASS: two-cashier Cash/Check/EWallet races; stale conflicts; DB uniqueness; daily ownership/cash; read-only reprint/search; authorized cancellation; non-reuse; immutable invoices.");
            var installmentPlan = new OtherCharge { BillingReferenceId = "A", Amount = 1000m, PayableIn = "4",
                InstallmentCount = 4, PaymentStatus = (int)PaymentStatus.Unpaid, DateCreated = DateTime.Now, DateUpdated = DateTime.Now };
            setup.OtherCharges.Add(installmentPlan); await setup.SaveChangesAsync();
            for (var sequence = 1; sequence <= 4; sequence++) setup.OtherChargeInstallments.Add(new OtherChargeInstallment {
                OtherChargeId = installmentPlan.Id, InstallmentNumber = sequence, Amount = 250m,
                DueBillingPeriod = new DateTime(2026, 11, 1).AddMonths(sequence - 1),
                PaymentStatus = (int)(sequence == 1 ? PaymentStatus.Unpaid : PaymentStatus.Draft),
                CreatedBy = 7, UpdatedBy = 7, DateCreated = DateTime.Now, DateUpdated = DateTime.Now });
            await setup.SaveChangesAsync(); setup.ChangeTracker.Clear();
            var installmentId = await setup.OtherChargeInstallments.Where(i => i.OtherChargeId == installmentPlan.Id && i.InstallmentNumber == 1).Select(i => i.Id).SingleAsync();
            var installmentPause = new PausePayment();
            async Task<string?> PayInstallment(int cashier, PausePayment? pause = null) {
                await using var installmentDb = new UserDbContext(Options(pause));
                try { return await new OtherChargeService(installmentDb).ProcessPayment(cashier, new() {
                    Method = PaymentMethod.Cash, AmountTendered = 250m, InstallmentIds = new() { installmentId } }); }
                catch (PaymentConflictException) { return null; }
            }
            var firstInstallment = PayInstallment(7, installmentPause);
            await installmentPause.Held.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var staleInstallment = PayInstallment(12);
            try { await Task.Delay(200); Check(!staleInstallment.IsCompleted, "Second cashier bypassed installment locks."); }
            finally { installmentPause.Release.TrySetResult(); }
            var installmentReceipt = await firstInstallment.WaitAsync(TimeSpan.FromSeconds(10));
            Check(installmentReceipt != null && await staleInstallment.WaitAsync(TimeSpan.FromSeconds(10)) == null,
                "Two cashiers settled one installment.");
            Check(await setup.OtherChargeInstallments.CountAsync(i => i.OtherChargeId == installmentPlan.Id && i.PaymentStatus == (int)PaymentStatus.Paid) == 1
                && await setup.OtherChargeInstallments.CountAsync(i => i.OtherChargeId == installmentPlan.Id && i.PaymentStatus == (int)PaymentStatus.Draft) == 3,
                "Cashier race settled future siblings.");
            await new PaymentService(setup).CancelInvoice(7, new() { InvoiceNo = installmentReceipt!, Reason = "Synthetic installment reversal" });
            setup.ChangeTracker.Clear();
            Check((await setup.Billings.SingleAsync(b => b.BillingReferenceId == "A")).RemainingAmount == 0m
                && (await setup.OtherCharges.SingleAsync(c => c.Id == installmentPlan.Id)).PaymentStatus == (int)PaymentStatus.Unpaid,
                "Installment reversal reopened paid water principal.");
            // Apply the exact migration twice to isolated synthetic tables, with a legacy parent retained.
            await setup.Database.ExecuteSqlRawAsync("DROP TABLE other_charge_installments");
            await setup.Database.ExecuteSqlRawAsync("ALTER TABLE other_charges DROP COLUMN InstallmentCount");
            var installmentMigration = Regex.Replace(await File.ReadAllTextAsync(Path.Combine(root,
                "TMCWD/TMCWD.Data/Database/20261008_add_other_charge_installments.sql")), @"(?m)^--.*$", "");
            for (var run = 0; run < 2; run++)
                foreach (var sql in installmentMigration.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    await setup.Database.ExecuteSqlRawAsync(sql);
            setup.ChangeTracker.Clear();
            Check(await setup.OtherCharges.CountAsync() == 2 && !await setup.OtherChargeInstallments.AnyAsync()
                && await setup.OtherCharges.AllAsync(c => c.InstallmentCount == null), "Migration converted or lost existing parents.");
            Console.WriteLine("PASS: real two-cashier installment locking, exact reversal, untouched water principal, and additive installment migration rerun without legacy backfill.");
        } finally {
            if (!Regex.IsMatch(prefix, @"^tm_pay_[a-f0-9]{16}_$")) throw new Exception("Unsafe synthetic cleanup target.");
            foreach (var table in created.AsEnumerable().Reverse()) await setup.Database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS `{table}`");
        }
    }
}
sealed class PausePayment(bool cancellation = false) : SaveChangesInterceptor
{
    public TaskCompletionSource Held { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
        if (data.Context!.ChangeTracker.Entries<Payment>().Any(e => e.State == EntityState.Added)
            || cancellation && data.Context.ChangeTracker.Entries<InvoiceCancellation>().Any(e => e.State == EntityState.Added)) { Held.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        return result;
    }
}
