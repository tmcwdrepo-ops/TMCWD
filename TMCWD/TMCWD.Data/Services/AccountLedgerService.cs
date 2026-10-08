using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Model;
using TMCWD.Model.Billing;
using Ledger = TMCWD.Model.Billing.AccountLedgerDto;

namespace TMCWD.Data.Services;

public sealed class AccountLedgerService(UserDbContext db, IBillingService billing)
{
    public async Task<Ledger?> Get(string accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber) || accountNumber.Trim().Length > 50)
            throw new ArgumentException("A valid account number is required.");
        accountNumber = accountNumber.Trim();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.AccountNumber == accountNumber);
        if (account == null) return null;
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == account.CustomerId);
        var result = new Ledger {
            Account = new Ledger.Header {
                AccountNumber = account.AccountNumber,
                AccountName = string.Join(" ", new[] { customer?.Firstname, customer?.Middlename, customer?.Lastname }.Where(s => !string.IsNullOrWhiteSpace(s))),
                ServiceAddress = string.Join(", ", new[] { account.UnitNumber, account.Building, account.HouseNumber, account.Street, account.Barangay }.Where(s => !string.IsNullOrWhiteSpace(s))),
                MeterNumber = account.MeterNumber,
                Classification = Enum.GetName(typeof(AccountClassification), account.Classification) ?? "",
                Status = Enum.GetName(typeof(AccountStatus), account.Status) ?? ""
            }
        };
        var bills = await db.Billings.AsNoTracking().Where(b => b.AccountId == account.Id)
            .OrderByDescending(b => b.BillingPeriod).ThenByDescending(b => b.Id).ToListAsync();
        var references = bills.Select(b => b.BillingReferenceId).ToList();
        var readings = await (from r in db.Readings.AsNoTracking()
            join s in db.ReadingSheets.AsNoTracking() on r.ReadingSheetId equals s.Id into sheets
            from s in sheets.DefaultIfEmpty()
            where r.AccountId == account.Id
            select new { r.Id, r.DateCreated, BillingDate = s == null ? (DateTime?)null : s.BillingDate }).ToDictionaryAsync(r => r.Id);
        var chargeService = new OtherChargeService(db);
        var types = await db.OtherFeeTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Name);
        foreach (var bill in bills)
        {
            var collection = await billing.GetCollectionBill(bill.BillingReferenceId)
                ?? throw new InvalidOperationException("A billing record is unavailable. Reload the ledger.");
            result.Bills.Add(new Ledger.Bill {
                BillingPeriod = bill.BillingPeriod,
                ReadingDate = readings.TryGetValue(bill.ReadingId, out var reading) ? reading.DateCreated : null,
                BillingDate = reading?.BillingDate,
                PaymentStatus = (PaymentStatus)bill.PaymentStatus, Collection = collection
            });
            result.TotalBalance += collection.AmountDue;
            result.CollectibleOtherCharges += collection.OtherCharges;
            if (bill.ReadingId > 0)
                Add("bill:" + bill.Id, bill.DateCreated, "Water Bill Posted", bill.BillingReferenceId,
                    bill.ServiceInvoiceNumber ?? "", collection.BillAmount, null, ((PaymentStatus)bill.PaymentStatus).ToString());
            foreach (var charge in await chargeService.GetByReference(bill.BillingReferenceId, true))
            {
                result.OtherCharges.Add(new Ledger.Charge {
                    Id = charge.Id, Description = types.GetValueOrDefault(charge.Type) ?? "Other Charge",
                    Particulars = charge.Particulars, ContractTotal = charge.Amount,
                    CollectibleNow = collection.CollectibleOtherCharges.Where(c => c.Id == charge.Id).Sum(c => c.Amount),
                    InstallmentCount = charge.InstallmentCount, PaymentStatus = (PaymentStatus)charge.PaymentStatus,
                    IsActive = charge.IsActive, Installments = charge.Installments
                });
            }
        }
        // Creation history includes inactive plans; their contracts never enter the collectible balance.
        var parents = await db.OtherCharges.AsNoTracking().Where(c => references.Contains(c.BillingReferenceId)).ToListAsync();
        foreach (var parent in parents)
            Add("charge:" + parent.Id, parent.DateCreated, "Other Charge Created", parent.BillingReferenceId,
                parent.Particulars + " (contract total " + parent.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ")",
                null, null, parent.IsActive ? ((PaymentStatus)parent.PaymentStatus).ToString() : "Inactive");
        var parentIds = parents.Select(c => c.Id).ToList();
        var children = await db.OtherChargeInstallments.AsNoTracking().Where(i => parentIds.Contains(i.OtherChargeId)).ToListAsync();
        foreach (var child in children.Where(i => i.ActivatedAt.HasValue))
            Add("activation:" + child.Id, child.ActivatedAt!.Value, "Installment Activated", child.ActivatedBillingReferenceId ?? "",
                "Charge " + child.OtherChargeId + ", installment #" + child.InstallmentNumber, child.Amount, null,
                ((PaymentStatus)child.PaymentStatus).ToString());
        foreach (var penalty in await db.Penalties.AsNoTracking().Where(p => references.Contains(p.BillingReferenceId)).ToListAsync())
            Add("penalty:" + penalty.Id, penalty.DateCreated, "Penalty Posted", penalty.BillingReferenceId, "Persisted penalty",
                penalty.Amount, null, ((PaymentStatus)penalty.PaymentStatus).ToString());
        var payments = await db.Payments.AsNoTracking().Where(p => references.Contains(p.BillingReference)).ToListAsync();
        var paymentReferences = payments.Select(p => p.PaymentReference).Where(r => !string.IsNullOrEmpty(r)).ToList();
        var cancellations = await db.InvoiceCancellations.AsNoTracking().Where(c => paymentReferences.Contains(c.PaymentReference)).ToListAsync();
        foreach (var group in payments.GroupBy(p => string.IsNullOrWhiteSpace(p.PaymentReference) ? "payment:" + p.Id : "receipt:" + p.PaymentReference))
        {
            var paymentIds = group.Select(p => p.Id).ToArray();
            var paidChildren = children.Where(i => i.SettledPaymentId.HasValue && paymentIds.Contains(i.SettledPaymentId.Value));
            var details = string.Join(", ", paidChildren.OrderBy(i => i.OtherChargeId).ThenBy(i => i.InstallmentNumber)
                .Select(i => "charge " + i.OtherChargeId + " installment #" + i.InstallmentNumber));
            Add("payment:" + group.Min(p => p.Id), group.Min(p => p.DateCreated), "Payment", group.First().PaymentReference ?? "",
                details.Length == 0 ? "Persisted account payment" : details, null, group.Sum(p => p.PaidAmount),
                group.All(p => p.Success) ? "Completed" : cancellations.Any(c => c.PaymentReference == group.First().PaymentReference) ? "Cancelled" : "Unsuccessful");
        }
        foreach (var cancellation in cancellations)
            Add("cancel:" + cancellation.Id, cancellation.DateCancelled, "Payment Cancellation", cancellation.PaymentReference,
                cancellation.Reason, payments.Where(p => p.PaymentReference == cancellation.PaymentReference).Sum(p => p.PaidAmount), null, "Cancelled");
        // Adjustment amounts are informational: type-specific amounts are already reflected in live water balances.
        foreach (var adjustment in await db.BillingAdjustments.AsNoTracking().Where(a => references.Contains(a.BillingReferenceId)).ToListAsync())
            Add("adjustment:" + adjustment.Id, adjustment.DateCreated, "Billing Adjustment", adjustment.BillingReferenceId,
                "Type " + adjustment.Type + ": " + adjustment.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), null, null, "Recorded");
        // ponytail: DCM persistence is undeployed; add its history source in Phase 6K after the authoritative schema and workflow are deployed.
        result.History = result.History.OrderByDescending(e => e.Date).ThenBy(e => e.Key, StringComparer.Ordinal).ToList();
        await transaction.CommitAsync();
        return result;

        void Add(string key, DateTime date, string type, string reference, string description, decimal? debit, decimal? credit, string status)
        {
            if (date == default) return; // A missing persisted timestamp cannot support a history event.
            result.History.Add(new Ledger.Event { Key = key, Date = date, Type = type, Reference = reference,
                Description = description, Debit = debit, Credit = credit, Status = status });
        }
    }
}
