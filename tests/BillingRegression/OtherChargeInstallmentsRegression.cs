using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;
using TMCWD.Model.Billing.Requests;

static class OtherChargeInstallmentsRegression
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static async Task Reject(Func<Task> action)
    {
        try { await action(); } catch (InvalidOperationException) { return; }
        throw new Exception("Expected safe rejection.");
    }

    public static async Task Run(UserDbContext db, ReadingSheetService sheets, Func<string, Func<Task>, Task> test)
    {
        var charges = new OtherChargeService(db);
        var payments = new PaymentService(db);
        var billingService = new BillingService(db);
        async Task<Billing> FirstBill(decimal currentReading = 0m)
        {
            var sheet = await db.ReadingSheets.SingleAsync();
            sheet.BillingDate = new DateTime(2026, 11, 2);
            sheet.DueDate = new DateTime(2026, 11, 17);
            db.Readings.Add(new Reading { AccountId = 1, ReadingSheetId = 1, CurrentReading = currentReading,
                Status = ReadingStatus.InProgress, DateCreated = new DateTime(2026, 11, 1) });
            await db.SaveChangesAsync();
            Check(await sheets.PartialPost(1, 1) == 1, "Initial water posting failed.");
            return await db.Billings.SingleAsync(b => b.ReadingId > 0);
        }
        async Task<OtherCharge> Plan(Billing bill, string count)
        {
            var charge = await charges.SaveUpdate(1, new OtherCharge {
                BillingReferenceId = bill.BillingReferenceId, Type = 1, Amount = 1000m, PayableIn = count });
            Check(!(await charges.GetCollectibleByReference(bill.BillingReferenceId)).Any(), "Draft entered Collections.");
            await charges.PostToCollection(1, new() { (int)charge.Id });
            db.ChangeTracker.Clear();
            return await db.OtherCharges.SingleAsync(c => c.Id == charge.Id);
        }
        async Task PostMonth(int sheetId, int month)
        {
            db.ReadingSheets.Add(new ReadingSheet { Id = sheetId, ZoneBookId = 1, Status = ReadingStatus.InProgress,
                BillingDate = new DateTime(2026 + (month - 1) / 12, (month - 1) % 12 + 1, 2) });
            db.Readings.Add(new Reading { AccountId = 1, ReadingSheetId = sheetId, CurrentReading = 0,
                Status = ReadingStatus.InProgress, DateCreated = new DateTime(2026 + (month - 1) / 12, (month - 1) % 12 + 1, 1) });
            await db.SaveChangesAsync();
            Check(await sheets.PartialPost(sheetId, 1) == 1 && await sheets.PartialPost(sheetId, 1) == 0,
                "Monthly posting/retry failed.");
            db.ChangeTracker.Clear();
        }
        await test("Installment count validation rejects blank/zero/negative/decimal/text on the server", async () => {
            foreach (var value in new string?[] { null, "", "0", "-1", "1.5", "3 months", "Cash", "Check", "GCash", "Maya", "Bank Transfer", "monthly", " 3", "+3", "2147483648" })
                await Reject(async () => await charges.SaveUpdate(1, new OtherCharge { PayableIn = value!, Amount = 1000m }));
            Check(!await db.OtherCharges.AnyAsync(), "Invalid counts left persisted charges.");
            foreach (var amount in new[] { 0m, -1m, 1.001m, 10000m })
                await Reject(async () => await charges.SaveUpdate(1, new OtherCharge { PayableIn = "1", Amount = amount }));
            await Reject(async () => await charges.SaveUpdate(1, new OtherCharge { PayableIn = "2", Amount = .01m }));
            try {
                System.Text.Json.JsonSerializer.Deserialize<OtherChargePaymentRequest>("{\"OtherChargeIds\":[1],\"InstallmentIds\":[1]}");
                throw new Exception("Obsolete parent payment field was accepted.");
            } catch (System.Text.Json.JsonException) { }
            await Reject(async () => await charges.ProcessPayment(1, new() { Method = PaymentMethod.Cash, AmountTendered = 1000m }));
        });
        await test("Posted parent without an exact installment ledger cannot be collected, reposted, or repaired implicitly", async () => {
            db.Billings.Add(new Billing { BillingReferenceId = "INVALID-PLAN", AccountId = 1, BillingPeriod = new DateTime(2026,11,1),
                PaymentStatus = (int)PaymentStatus.Unpaid, RemainingAmount = 190m, TotalBillAmount = 190m,
                BillArrearsAmount = 207m, InvoiceSnapshotJson = "{\"BillAmount\":170}" });
            var parent = new OtherCharge { BillingReferenceId = "INVALID-PLAN", Amount = 1000m, PayableIn = "Cash",
                PaymentStatus = (int)PaymentStatus.Unpaid };
            db.OtherCharges.Add(parent); await db.SaveChangesAsync();
            await Reject(async () => await charges.GetCollectibleByReference("INVALID-PLAN"));
            await Reject(async () => await billingService.GetCollectionBill("INVALID-PLAN"));
            await Reject(async () => await charges.GetByReference("INVALID-PLAN"));
            await Reject(async () => await charges.PostToCollection(1, new() { (int)parent.Id }));
            db.ChangeTracker.Clear();
            parent = await db.OtherCharges.SingleAsync(); parent.PayableIn = "2"; parent.InstallmentCount = 2;
            db.OtherChargeInstallments.Add(new OtherChargeInstallment { OtherChargeId = parent.Id, InstallmentNumber = 1,
                DueBillingPeriod = new DateTime(2026,11,1), Amount = 500m, PaymentStatus = (int)PaymentStatus.Unpaid });
            await db.SaveChangesAsync();
            await Reject(async () => await charges.GetCollectibleByReference("INVALID-PLAN"));
            parent.InstallmentCount = 1; await db.SaveChangesAsync();
            await Reject(async () => await charges.GetCollectibleByReference("INVALID-PLAN")); // Amount still does not reconcile.
            Check(await db.Payments.CountAsync() == 0 && await db.OtherChargeInstallments.CountAsync() == 1
                && (await db.Billings.SingleAsync()).RemainingAmount == 190m, "Invalid parent changed financial state.");
        });
        await test("New one-time charges use one child; installment plans reject synthetic anchors", async () => {
            db.Billings.Add(new Billing { BillingReferenceId = "STUB", AccountId = 1, BillingPeriod = new DateTime(2026, 11, 1),
                PaymentStatus = (int)PaymentStatus.Unpaid });
            await db.SaveChangesAsync();
            var plan = await charges.SaveUpdate(1, new OtherCharge { BillingReferenceId = "STUB", Type = 1, Amount = 1000m, PayableIn = "4" });
            await Reject(async () => await charges.PostToCollection(1, new() { (int)plan.Id }));
            db.ChangeTracker.Clear();
            Check(!await db.OtherChargeInstallments.AnyAsync() && (await db.OtherCharges.SingleAsync()).PaymentStatus == (int)PaymentStatus.Draft,
                "Stub plan activated or lost its draft.");
            var once = await charges.SaveUpdate(1, new OtherCharge { BillingReferenceId = "STUB", Type = 1, Amount = 1000m, PayableIn = "1" });
            await charges.PostToCollection(1, new() { (int)once.Id });
            Check(await db.OtherChargeInstallments.CountAsync() == 1 && (await db.OtherChargeInstallments.SingleAsync()).Amount == 1000m,
                "One-time charge did not create exactly one installment.");
            Check((await billingService.GetCollectionBill("STUB"))?.AmountDue == 1000m && (await db.Billings.SingleAsync()).RemainingAmount == 0m,
                "One-time stub changed the water balance.");
            var receipt = await charges.ProcessPayment(1, new() { Method = PaymentMethod.Cash, AmountTendered = 1000m,
                InstallmentIds = new() { (await db.OtherChargeInstallments.SingleAsync()).Id } });
            await payments.CancelInvoice(1, new() { InvoiceNo = receipt, Reason = "Synthetic reversal" });
            db.ChangeTracker.Clear();
            Check((await db.Billings.SingleAsync()).RemainingAmount == 0m && (await billingService.GetCollectionBill("STUB"))?.AmountDue == 1000m,
                "One-time reversal changed water balance or failed to restore the installment.");
        });
        await test("Four installments reconcile; posting/payment/cancellation preserve invoice and water state", async () => {
            var bill = await FirstBill();
            var frozen = bill.InvoiceSnapshotJson;
            var water = bill.RemainingAmount;
            var arrears = bill.BillArrearsAmount;
            var plan = await Plan(bill, "4");
            await charges.PostToCollection(1, new() { (int)plan.Id });
            var children = await db.OtherChargeInstallments.OrderBy(i => i.InstallmentNumber).ToListAsync();
            Check(children.Count == 4 && children.All(i => i.Amount == 250m) && children.Sum(i => i.Amount) == 1000m,
                "Four-installment amount/identity mismatch.");
            Check(children[0].PaymentStatus == (int)PaymentStatus.Unpaid && children.Skip(1).All(i => i.PaymentStatus == (int)PaymentStatus.Draft)
                && children.Select(i => i.DueBillingPeriod).SequenceEqual(Enumerable.Range(0, 4).Select(n => new DateTime(2026, 11, 1).AddMonths(n))),
                "Starting/future installment states or monthly periods are wrong.");
            Check((await billingService.GetCollectionBill(bill.BillingReferenceId))?.AmountDue == water + 250m,
                "Collections double-counted the parent or exposed future installments.");
            foreach (var selected in new List<long>[] { new(), new() { children[1].Id }, new() { children[0].Id, children[0].Id } })
                await Reject(async () => await payments.ProcessCollectionPayment(1, new() { Method = PaymentMethod.Cash,
                    AmountTendered = water + 250m, Bills = new() { new() { BillingReferenceId = bill.BillingReferenceId,
                        PaidAmount = water + 250m, InstallmentIds = selected } } }));
            db.ChangeTracker.Clear();
            await Reject(async () => await charges.ProcessPayment(1, new() { Method = PaymentMethod.Cash, AmountTendered = 1000m,
                InstallmentIds = new() }));
            await Reject(async () => await charges.ProcessPayment(1, new() { Method = PaymentMethod.Cash, AmountTendered = 250m,
                InstallmentIds = new() { children[1].Id } }));
            await PostMonth(2, 12);
            Check((await charges.GetCollectibleByReference(bill.BillingReferenceId)).Sum(c => c.Amount) == 500m,
                "Prior unpaid installment was lost or future installment activated early.");
            var receipt = await charges.ProcessPayment(1, new() { Method = PaymentMethod.Cash, AmountTendered = 250m,
                InstallmentIds = new() { children[1].Id } });
            await Reject(async () => await charges.ProcessPayment(1, new() { Method = PaymentMethod.Cash, AmountTendered = 250m,
                InstallmentIds = new() { children[1].Id } }));
            db.ChangeTracker.Clear();
            Check((await db.OtherCharges.SingleAsync()).PaymentStatus == (int)PaymentStatus.Unpaid
                && (await charges.GetCollectibleByReference(bill.BillingReferenceId)).Sum(c => c.Amount) == 250m,
                "Parent marked paid or paid installment reappeared.");
            await payments.CancelInvoice(1, new() { InvoiceNo = receipt, Reason = "Synthetic correction" });
            await Reject(async () => await payments.CancelInvoice(1, new() { InvoiceNo = receipt, Reason = "Repeat correction" }));
            db.ChangeTracker.Clear();
            Check((await charges.GetCollectibleByReference(bill.BillingReferenceId)).Sum(c => c.Amount) == 500m
                && await db.OtherChargeInstallments.CountAsync(i => i.PaymentStatus == (int)PaymentStatus.Draft) == 2,
                "Cancellation reopened future siblings or duplicated installments.");
            var unchanged = await db.Billings.SingleAsync(b => b.Id == bill.Id);
            Check(unchanged.RemainingAmount == water && unchanged.BillArrearsAmount == arrears && unchanged.InvoiceSnapshotJson == frozen,
                "Installments changed the water balance, arrears, or frozen invoice.");
            await PostMonth(3, 14); // Delayed January billing; February catches up both remaining rows.
            Check((await charges.GetCollectibleByReference(bill.BillingReferenceId)).Sum(c => c.Amount) == 1000m
                && await db.OtherChargeInstallments.CountAsync() == 4, "Catch-up lost or duplicated installments.");
            await charges.ProcessPayment(1, new() { Method = PaymentMethod.Cash, AmountTendered = 1000m,
                InstallmentIds = children.Select(i => i.Id).ToList() });
            db.ChangeTracker.Clear();
            Check((await db.OtherCharges.SingleAsync()).PaymentStatus == (int)PaymentStatus.Paid
                && !(await charges.GetCollectibleByReference(bill.BillingReferenceId)).Any(), "Fully paid parent/children remain collectible.");
            var postedParent = await db.OtherCharges.SingleAsync();
            Check(postedParent.InstallmentCount >= 1 && await db.OtherChargeInstallments.CountAsync(i => i.OtherChargeId == postedParent.Id) == postedParent.InstallmentCount,
                "Posted-plan count invariant failed.");
        });
        await test("Three-installment cents; paid water anchors; exact sibling cancellation; parent metadata history", async () => {
            var bill = await FirstBill();
            var frozen = bill.InvoiceSnapshotJson;
            var plan = await Plan(bill, "3");
            var children = await db.OtherChargeInstallments.OrderBy(i => i.InstallmentNumber).ToListAsync();
            Check(children.Select(i => i.Amount).SequenceEqual(new[] { 333.33m, 333.33m, 333.34m }) && children.Sum(i => i.Amount) == 1000m,
                "Three-installment cents did not reconcile.");
            await payments.ProcessCollectionPayment(1, new CollectionPaymentRequest { Method = PaymentMethod.Cash,
                AmountTendered = bill.RemainingAmount + 333.33m,
                Bills = new() { new() { BillingReferenceId = bill.BillingReferenceId, PaidAmount = bill.RemainingAmount + 333.33m, InstallmentIds = new() { children[0].Id } } } });
            db.ChangeTracker.Clear();
            Check((await db.OtherCharges.SingleAsync()).PaymentStatus == (int)PaymentStatus.Unpaid
                && (await db.OtherChargeInstallments.SingleAsync(i => i.Id == children[0].Id)).PaymentStatus == (int)PaymentStatus.Paid,
                "Combined collection settled scheduled siblings.");
            await PostMonth(2, 12);
            Check((await billingService.GetCollectionBill(bill.BillingReferenceId))?.AmountDue == 333.33m,
                "Settled water bill hid its newly activated installment.");
            var receipt = (await payments.ProcessCollectionPayment(1, new CollectionPaymentRequest { Method = PaymentMethod.Cash,
                AmountTendered = 333.33m, Bills = new() { new() { BillingReferenceId = bill.BillingReferenceId, PaidAmount = 333.33m, InstallmentIds = new() { children[1].Id } } } })).Single().PaymentReference;
            await payments.CancelInvoice(1, new() { InvoiceNo = receipt, Reason = "Synthetic correction" });
            db.ChangeTracker.Clear();
            children = await db.OtherChargeInstallments.OrderBy(i => i.InstallmentNumber).ToListAsync();
            Check(children[0].PaymentStatus == (int)PaymentStatus.Paid && children[1].PaymentStatus == (int)PaymentStatus.Unpaid
                && children[2].PaymentStatus == (int)PaymentStatus.Draft, "Cancellation restored siblings instead of the linked installment.");
            var waterBill = await db.Billings.SingleAsync(b => b.Id == bill.Id);
            Check(waterBill.PaymentStatus == (int)PaymentStatus.Paid && waterBill.RemainingAmount == 0m && waterBill.InvoiceSnapshotJson == frozen,
                "Installment-only collection/cancellation reopened the water bill or changed its snapshot.");
            var metadata = (await charges.GetByReference(bill.BillingReferenceId)).Single();
            Check(metadata.Amount == 1000m && metadata.InstallmentCount == 3 && metadata.Installments.Count == 3
                && metadata.Installments[0].PaymentReference != null && metadata.Installments[1].PaymentReference == null,
                "Plan metadata/child settlement history was lost.");
            plan = await db.OtherCharges.SingleAsync(c => c.Id == plan.Id); plan.IsActive = false;
            await db.SaveChangesAsync();
            Check((await charges.GetCollectibleByReference(bill.BillingReferenceId)).Sum(c => c.Amount) == 0m,
                "Inactive plan remains collectible.");
        });
        await test("397 water/penalty + 250 installment = 647 exactly once; combined receipt and reversal", async () => {
            var priorSnapshot = System.Text.Json.JsonSerializer.Serialize(new TMCWD.Model.Billing.OfficialInvoiceSnapshot(
                1, null, new DateTime(2026, 10, 1), 170m, 20m, null, 0m, 0m, 0m, 190m,
                Array.Empty<TMCWD.Model.Billing.InvoiceChargeLine>()));
            db.Billings.Add(new Billing { AccountId = 1, BillingReferenceId = "OCTOBER", BillingPeriod = new DateTime(2026, 10, 2),
                TotalBillAmount = 190m, RemainingAmount = 190m, PaymentStatus = (int)PaymentStatus.Unpaid,
                InvoiceSnapshotJson = priorSnapshot, DateCreated = new DateTime(2026, 10, 2) });
            db.Penalties.Add(new Penalty { BillingReferenceId = "OCTOBER", Amount = 17m,
                PaymentStatus = (int)PaymentStatus.Unpaid, DateCreated = new DateTime(2026, 10, 20), DateUpdated = new DateTime(2026, 10, 20) });
            await db.SaveChangesAsync();
            var bill = await FirstBill(17m);
            Check(bill.RemainingAmount == 190m && bill.BillArrearsAmount == 207m, "Water-side fixture/arrears mismatch.");
            var frozen = bill.InvoiceSnapshotJson;
            await Plan(bill, "4");
            var october = (await billingService.GetCollectionBill("OCTOBER"))!;
            var november = (await billingService.GetCollectionBill(bill.BillingReferenceId))!;
            Check(october.AmountDue == 207m && november.AmountDue == 440m && october.AmountDue + november.AmountDue == 647m
                && november.OtherCharges == 250m && november.CollectibleOtherCharges.Sum(c => c.Amount) == 250m,
                "Collection breakdown/rows do not reconcile to 647.");
            var receipt = (await payments.ProcessCollectionPayment(1, new() { Method = PaymentMethod.Cash, AmountTendered = 647m,
                Bills = new() { new() { BillingReferenceId = "OCTOBER", PaidAmount = 207m },
                    new() { BillingReferenceId = bill.BillingReferenceId, PaidAmount = 440m, InstallmentIds = new() { (await db.OtherChargeInstallments.FirstAsync()).Id } } } })).First().PaymentReference;
            db.ChangeTracker.Clear();
            Check(await db.OtherChargeInstallments.CountAsync(i => i.PaymentStatus == (int)PaymentStatus.Paid) == 1
                && await db.OtherChargeInstallments.CountAsync(i => i.PaymentStatus == (int)PaymentStatus.Draft) == 3
                && (await db.OtherCharges.SingleAsync()).SettledPaymentId == null, "Combined receipt settled the parent/future installments.");
            await payments.CancelInvoice(1, new() { InvoiceNo = receipt, Reason = "Synthetic combined reversal" });
            db.ChangeTracker.Clear();
            var restored = await db.Billings.SingleAsync(b => b.Id == bill.Id);
            Check(restored.RemainingAmount == 190m && restored.BillArrearsAmount == 207m && restored.InvoiceSnapshotJson == frozen
                && (await db.Billings.SingleAsync(b => b.BillingReferenceId == "OCTOBER")).InvoiceSnapshotJson == priorSnapshot
                && (await billingService.GetCollectionBill("OCTOBER"))!.AmountDue + (await billingService.GetCollectionBill(bill.BillingReferenceId))!.AmountDue == 647m,
                "Combined reversal changed immutable invoices/arrears or copied installments into principal.");
        });
    }
}
