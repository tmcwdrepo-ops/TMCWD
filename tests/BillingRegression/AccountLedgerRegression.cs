using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;
using TMCWD.Model.Billing.Requests;

static class AccountLedgerRegression
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static async Task Run(UserDbContext db, Func<string, Func<Task>, Task> test)
    {
        var billing = new BillingService(db);
        var ledger = new AccountLedgerService(db, billing);
        var charges = new OtherChargeService(db);
        var payments = new PaymentService(db);
        var stamp = new DateTime(2026, 11, 1, 9, 30, 0);
        async Task Seed()
        {
            var account = await db.Accounts.SingleAsync(); account.MeterNumber = "LEDGER-METER"; account.Status = 3;
            account.Street = "Synthetic Street";
            var sheet = await db.ReadingSheets.SingleAsync(); sheet.BillingDate = stamp.Date.AddDays(1); sheet.DueDate = stamp.Date.AddDays(16);
            db.Readings.Add(new Reading { Id = 101, AccountId = 1, ReadingSheetId = 1, Status = ReadingStatus.Completed, DateCreated = stamp });
            db.Billings.Add(new Billing { Id = 101, AccountId = 1, ReadingId = 101, BillingReferenceId = "LEDGER-1",
                BillingPeriod = stamp.Date, DateCreated = stamp.AddHours(1), RemainingAmount = 190m, TotalBillAmount = 9999m,
                BillArrearsAmount = 8000m, PaymentStatus = (int)PaymentStatus.Unpaid, ServiceInvoiceNumber = "LEDGER-SI",
                InvoiceSnapshotJson = "{\"BillAmount\":170,\"MeterMaintenance\":20,\"TotalDue\":9999}" });
            db.OtherCharges.Add(new OtherCharge { Id = 101, Type = 1, BillingReferenceId = "LEDGER-1", Amount = 1000m,
                PayableIn = "4", InstallmentCount = 4, PaymentStatus = (int)PaymentStatus.Unpaid, IsActive = true,
                Particulars = "<img src=x onerror=alert(1)>", DateCreated = stamp.AddHours(2) });
            for (var n = 4; n >= 1; --n) db.OtherChargeInstallments.Add(new OtherChargeInstallment {
                Id = 1000 + n, OtherChargeId = 101, InstallmentNumber = n, DueBillingPeriod = stamp.Date.AddMonths(n - 1),
                Amount = 250m, PaymentStatus = n == 1 ? (int)PaymentStatus.Unpaid : (int)PaymentStatus.Draft,
                ActivatedAt = n == 1 ? stamp.AddHours(3) : null, ActivatedBillingReferenceId = n == 1 ? "LEDGER-1" : null,
                DateCreated = stamp.AddHours(2), DateUpdated = stamp.AddHours(3) });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        }
        async Task<TMCWD.Model.Billing.AccountLedgerDto> Get() => await ledger.Get("TEST") ?? throw new Exception("Ledger missing.");
        async Task Case(string name, Func<Task> action) => await test("Account Ledger: " + name, async () => { await Seed(); await action(); });
        await Case("authoritative account header", async () => {
            var x = await Get(); Check(x.Account.AccountName == "Senior Citizen Tester" && x.Account.AccountNumber == "TEST"
                && x.Account.MeterNumber == "LEDGER-METER" && x.Account.ServiceAddress == "Synthetic Street"
                && x.Account.Classification == "Residential" && x.Account.Status == "Active" && x.Account.RateCode == null, "Header invented or lost data.");
        });
        await Case("persisted water bill and exact reading", async () => {
            var b = (await Get()).Bills.Single(); Check(b.ReadingDate == stamp && b.Collection.BillAmount == 170m
                && b.Collection.MeterMaintenanceFee == 20m && b.Collection.ServiceInvoiceNumber == "LEDGER-SI"
                && b.Collection.DueDate == stamp.Date.AddDays(16) && b.BillingDate == stamp.Date.AddDays(1), "Water bill lost authoritative values.");
        });
        await Case("ordered installment metadata and activation reference", async () => {
            var c = (await Get()).OtherCharges.Single(); Check(c.InstallmentCount == 4 && c.ContractTotal == 1000m
                && c.Installments.Select(i => i.InstallmentNumber).SequenceEqual(new[] { 1, 2, 3, 4 })
                && c.Installments[0].ActivatedBillingReferenceId == "LEDGER-1", "Plan ordering/metadata incorrect.");
        });
        await Case("future installments excluded", async () => Check((await Get()).CollectibleOtherCharges == 250m, "Future included."));
        await Case("unpaid installment counted once", async () => {
            var x = await Get(); Check(x.TotalBalance == 440m && x.OtherCharges.Single().CollectibleNow == 250m, "Unpaid child duplicated.");
        });
        await Case("paid installments contribute zero", async () => {
            await db.OtherChargeInstallments.Where(i => i.Id == 1001).ExecuteUpdateAsync(s => s.SetProperty(i => i.PaymentStatus, (int)PaymentStatus.Paid));
            Check((await Get()).CollectibleOtherCharges == 0m, "Paid child collectible.");
        });
        await Case("contract and snapshot arrears do not inflate balance", async () => Check((await Get()).TotalBalance == 440m, "Contract/arrears double counted."));
        await Case("Collections and Charging monetary reconciliation", async () => {
            var x = await Get(); var c = await billing.GetCollectionBill("LEDGER-1");
            var rows = await charges.GetCollectibleByReference("LEDGER-1");
            Check(x.TotalBalance == c!.AmountDue && x.CollectibleOtherCharges == c.OtherCharges
                && x.CollectibleOtherCharges == rows.Sum(i => i.Amount), "Ledger/Collections/workspace disagree.");
        });
        await Case("history uses persisted timestamps and stable chronology", async () => {
            var x = await Get(); Check(x.History.Single(e => e.Type == "Installment Activated").Date == stamp.AddHours(3)
                && x.History.Count(e => e.Type == "Installment Activated") == 1
                && x.History.Select(e => e.Date).SequenceEqual(x.History.Select(e => e.Date).OrderByDescending(d => d)), "History guessed or unordered.");
        });
        await Case("repeated reads are side effect free", async () => {
            var before = await db.OtherChargeInstallments.AsNoTracking().OrderBy(i => i.Id).ToListAsync();
            await Get(); await Get();
            var after = await db.OtherChargeInstallments.AsNoTracking().OrderBy(i => i.Id).ToListAsync();
            Check(System.Text.Json.JsonSerializer.Serialize(before) == System.Text.Json.JsonSerializer.Serialize(after)
                && !await db.Payments.AnyAsync() && !await db.Penalties.AnyAsync(), "Ledger GET mutated finance.");
        });
        await Case("invalid and missing accounts fail safely", async () => {
            Check(await ledger.Get("MISSING") == null, "Unknown account exposed data.");
            foreach (var number in new[] { "", " ", new string('x', 51) }) {
                try { await ledger.Get(number); throw new Exception("Invalid account accepted."); } catch (ArgumentException) { }
            }
        });
        await Case("payment reload retains exact child and receipt", async () => {
            var receipt = await charges.ProcessPayment(1, new() { Method = PaymentMethod.Cash, AmountTendered = 250m, InstallmentIds = new() { 1001 } });
            db.ChangeTracker.Clear(); var x = await Get();
            Check(x.TotalBalance == 190m && x.OtherCharges.Single().Installments[0].PaymentReference == receipt
                && x.History.Single(e => e.Type == "Payment").Description.Contains("installment #1"), "Payment ledger stale or lost exact child.");
        });
        await Case("cancellation reload retains original payment and reversal", async () => {
            var receipt = await charges.ProcessPayment(1, new() { Method = PaymentMethod.Cash, AmountTendered = 250m, InstallmentIds = new() { 1001 } });
            await payments.CancelInvoice(1, new() { InvoiceNo = receipt, Reason = "Synthetic ledger reversal" });
            db.ChangeTracker.Clear(); var x = await Get();
            Check(x.TotalBalance == 440m && x.History.Single(e => e.Type == "Payment").Status == "Cancelled"
                && x.History.Single(e => e.Type == "Payment Cancellation").Debit == 250m
                && !x.History.Single(e => e.Type == "Payment").Description.Contains("installment #"), "Cancellation fabricated a cleared settlement or lost payment.");
        });
        await Case("immutable snapshots and persisted penalty source", async () => {
            var frozen = (await db.Billings.AsNoTracking().SingleAsync()).InvoiceSnapshotJson;
            db.Penalties.Add(new Penalty { BillingReferenceId = "LEDGER-1", Amount = 17m, PaymentStatus = (int)PaymentStatus.Unpaid, DateCreated = stamp.AddDays(17) });
            await db.SaveChangesAsync(); await Get();
            Check((await Get()).TotalBalance == 457m && (await db.Billings.AsNoTracking().SingleAsync()).InvoiceSnapshotJson == frozen, "GET rewrote snapshot or recalculated penalty.");
        });
        await Case("inactive plans and unrelated accounts excluded", async () => {
            await db.OtherCharges.Where(c => c.Id == 101).ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, false));
            var x = await Get(); Check(x.TotalBalance == 190m && x.CollectibleOtherCharges == 0m && !x.OtherCharges.Single().IsActive
                && x.OtherCharges.Single().CollectibleNow == 0m
                && x.History.Any(e => e.Type == "Other Charge Created" && e.Status == "Inactive"), "Inactive plan collected or history lost.");
        });
        await test("Account Ledger: empty deployed transaction set succeeds without querying DCM", async () => {
            var guard = new NoDcmQuery();
            await using var readDb = new UserDbContext(new DbContextOptionsBuilder<UserDbContext>()
                .UseMySql(db.Database.GetDbConnection(), new MySqlServerVersion(new Version(8, 0, 0))).AddInterceptors(guard).Options);
            var source = new AccountLedgerService(readDb, new BillingService(readDb));
            var x = await source.Get("TEST") ?? throw new Exception("Empty account failed.");
            Check(x.Account.AccountNumber == "TEST" && x.TotalBalance == 0m && x.CollectibleOtherCharges == 0m
                && x.Bills.Count == 0 && x.OtherCharges.Count == 0 && x.History.Count == 0 && guard.ReadCount > 0,
                "Empty collections/header/balance incorrect.");
            Check(!await db.Billings.AnyAsync() && !await db.OtherCharges.AnyAsync() && !await db.Payments.AnyAsync(), "Empty GET mutated database state.");
        });
        await Case("populated deployed history loads without querying DCM", async () => {
            var guard = new NoDcmQuery();
            await using var readDb = new UserDbContext(new DbContextOptionsBuilder<UserDbContext>()
                .UseMySql(db.Database.GetDbConnection(), new MySqlServerVersion(new Version(8, 0, 0))).AddInterceptors(guard).Options);
            var receipt = await charges.ProcessPayment(1, new() { Method = PaymentMethod.Cash, AmountTendered = 250m, InstallmentIds = new() { 1001 } });
            var x = await new AccountLedgerService(readDb, new BillingService(readDb)).Get("TEST") ?? throw new Exception("Populated account failed.");
            Check(x.Bills.Count == 1 && x.OtherCharges.Count == 1 && x.History.Any(e => e.Type == "Payment" && e.Reference == receipt)
                && x.TotalBalance == 190m && guard.ReadCount > 0, "Supported finance/history lost with DCM absent.");
        });
        await Case("missing invoice and activation dates are never inferred", async () => {
            await db.OtherChargeInstallments.Where(i => i.Id == 1001).ExecuteUpdateAsync(s => s.SetProperty(i => i.ActivatedAt, (DateTime?)null));
            await db.Billings.Where(b => b.Id == 101).ExecuteUpdateAsync(s => s.SetProperty(b => b.ReadingId, 0));
            var x = await Get(); Check(x.Bills.Single().ReadingDate == null && x.Bills.Single().BillingDate == null
                && !x.History.Any(e => e.Type is "Installment Activated" or "Water Bill Posted"), "History or dates inferred from state.");
        });
        await Case("inconsistent posted plans are rejected without repair", async () => {
            await db.OtherChargeInstallments.Where(i => i.Id == 1004).ExecuteDeleteAsync();
            try { await Get(); throw new Exception("Broken plan accepted."); } catch (InvalidOperationException) { }
            Check(await db.OtherChargeInstallments.CountAsync() == 3 && (await db.Billings.SingleAsync()).RemainingAmount == 190m, "GET repaired or changed finance.");
        });
        await Case("unreferenced payments retain separate timestamps without invented cancellation", async () => {
            db.Payments.AddRange(new Payment { BillingReference = "LEDGER-1", PaidAmount = 10m, Success = false, DateCreated = stamp.AddDays(1) },
                new Payment { BillingReference = "LEDGER-1", PaidAmount = 20m, Success = true, DateCreated = stamp.AddDays(2) });
            await db.SaveChangesAsync(); var events = (await Get()).History.Where(e => e.Type == "Payment").ToList();
            Check(events.Count == 2 && events.Select(e => e.Credit).SequenceEqual(new decimal?[] { 20m, 10m })
                && events.Last().Status == "Unsuccessful" && !events.Any(e => e.Status == "Cancelled"), "Unrelated payments collapsed or cancellation guessed.");
        });
    }

    sealed class NoDcmQuery : DbCommandInterceptor
    {
        public int ReadCount { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("debit_credit_memos", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Account Ledger queried undeployed DCM persistence.");
            ++ReadCount;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
