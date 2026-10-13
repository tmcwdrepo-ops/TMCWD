using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
using TMCWD.Model.Billing;
using BillingEntity = TMCWD.Data.Entities.Billing;

namespace TMCWD.Data.Services;

public sealed class DisconnectionMonitoringService(UserDbContext db, IBillingService billing)
{
    public async Task<DisconnectionMonitoringDto?> Get(string accountNumber, DateOnly today)
    {
        ValidateAccount(accountNumber);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.AccountNumber == accountNumber.Trim());
        var result = account == null ? null : await Evaluate(account, today);
        await transaction.CommitAsync();
        return result;
    }

    public async Task<List<DisconnectionMonitoringDto>> List(string? query, int? zone, int? book, DateOnly today)
    {
        var q = (query ?? "").Trim();
        if (q.Length > 100 || zone < 0 || book < 0) throw new ArgumentException("Invalid monitoring filter.");
        var numbers = await (from a in db.Accounts.AsNoTracking()
            join c in db.Customers.AsNoTracking() on a.CustomerId equals c.Id
            join z in db.ZoneBooks.AsNoTracking() on a.ZoneBookId equals z.Id
            where (q == "" || a.AccountNumber.Contains(q) || c.Firstname.Contains(q) || c.Lastname.Contains(q))
                && (!zone.HasValue || z.Zone == zone) && (!book.HasValue || z.Book == book)
            orderby a.AccountNumber select a.AccountNumber).Take(101).ToListAsync();
        if (numbers.Count > 100) throw new ArgumentException("Narrow the search to at most 100 accounts.");
        // ponytail: cap matches at 100 to bound existing collection reads; paginate/batch when larger deployments require it.
        var result = new List<DisconnectionMonitoringDto>();
        foreach (var number in numbers) {
            var row = await Get(number, today);
            if (row != null) result.Add(row);
        }
        return result;
    }

    async Task<DisconnectionMonitoringDto> Evaluate(Account account, DateOnly today)
    {
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == account.CustomerId);
        var zone = await db.ZoneBooks.AsNoTracking().SingleOrDefaultAsync(z => z.Id == account.ZoneBookId);
        var result = new DisconnectionMonitoringDto {
            AccountNumber = account.AccountNumber, CustomerId = account.CustomerId, MeterNumber = account.MeterNumber,
            AccountName = string.Join(" ", new[] { customer?.Firstname, customer?.Middlename, customer?.Lastname }.Where(s => !string.IsNullOrWhiteSpace(s))),
            ServiceAddress = string.Join(", ", new[] { account.UnitNumber, account.Building, account.HouseNumber, account.Street, account.Barangay }.Where(s => !string.IsNullOrWhiteSpace(s))),
            Zone = zone?.Zone ?? 0, Book = zone?.Book ?? 0,
            OperationalAccountStatus = Enum.GetName(typeof(AccountStatus), account.Status) ?? "Unknown"
        };
        var bills = await db.Billings.AsNoTracking().Where(b => b.AccountId == account.Id).ToListAsync();
        var collections = new Dictionary<string, CollectionBill>();
        foreach (var bill in bills) {
            var collection = await billing.GetCollectionBill(bill.BillingReferenceId)
                ?? throw new InvalidOperationException("A bill is unavailable.");
            collections.Add(bill.BillingReferenceId, collection);
            result.OutstandingWaterBalance += collection.RemainingAmount;
            result.ActivePenaltyBalance += collection.Penalty;
            result.CollectibleOtherCharges += collection.OtherCharges;
            result.TotalCollectibleBalance += collection.AmountDue;
        }
        // PartialPost persists issuance in BillingPeriod. A charge anchor has no completed reading.
        var issuedBefore = today.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var monthly = await (from b in db.Billings.AsNoTracking()
            join r in db.Readings.AsNoTracking() on b.ReadingId equals r.Id
            join s in db.ReadingSheets.AsNoTracking() on r.ReadingSheetId equals s.Id
            where b.AccountId == account.Id && r.AccountId == account.Id && r.Status == ReadingStatus.Completed
                && s.Status != ReadingStatus.Deleted && b.BillingPeriod != default
                && b.BillingPeriod < issuedBefore
            orderby b.BillingPeriod select b).ToListAsync();
        if (monthly.GroupBy(b => (b.BillingPeriod.Year, b.BillingPeriod.Month)).Any(g => g.Count() != 1)) {
            result.Status = "FINANCIAL / SERVICE STATE REVIEW REQUIRED";
            result.FinancialReviewRequired = true;
            return result;
        }
        BillingEntity? first = null, second = null;
        for (var i = 1; i < monthly.Count; ++i) {
            var previous = monthly[i - 1]; var next = monthly[i];
            if (new DateTime(previous.BillingPeriod.Year, previous.BillingPeriod.Month, 1).AddMonths(1)
                    == new DateTime(next.BillingPeriod.Year, next.BillingPeriod.Month, 1)
                && collections[previous.BillingReferenceId].RemainingAmount > 0) {
                first = previous; second = next; break;
            }
        }
        first ??= monthly.FirstOrDefault(b => collections[b.BillingReferenceId].RemainingAmount > 0);
        if (first != null) {
            result.FirstBillingReference = first.BillingReferenceId;
            result.FirstBillingDate = first.BillingPeriod.Date;
            result.FirstBillingRemainingAmount = collections[first.BillingReferenceId].RemainingAmount;
            result.Status = "FIRST BILL UNPAID";
        }
        if (second != null) {
            result.SecondBillingReference = second.BillingReferenceId;
            result.SecondBillingDate = second.BillingPeriod.Date;
            result.SecondBillingRemainingAmount = collections[second.BillingReferenceId].RemainingAmount;
            result.SubjectDate = second.BillingPeriod.Date;
            result.ScheduledDisconnectionDate = second.BillingPeriod.Date.AddDays(5);
            result.DaysRemaining = Math.Max(0, DateOnly.FromDateTime(result.ScheduledDisconnectionDate.Value).DayNumber - today.DayNumber);
            result.Status = result.DaysRemaining == 0 ? "FOR DISCONNECTION" : "SUBJECT FOR DISCONNECTION";
        }
        var physical = await GetLatestPhysicalEvent(db, account.Id);
        if (physical == null) {
            // Suspended/Closed and legacy orders do not establish physical-event evidence.
            if (account.Status != (int)AccountStatus.Active) {
                result.Status = "FINANCIAL / SERVICE STATE REVIEW REQUIRED"; result.FinancialReviewRequired = true;
            }
            return result;
        }
        result.EventId = physical.Id;
        result.ActualDisconnectionDateUtc = physical.ActualDisconnectionDateUtc;
        result.ActualReconnectionDateUtc = physical.ActualReconnectionDateUtc;
        result.PhysicalServiceStatus = physical.ActualReconnectionDateUtc.HasValue ? "RECONNECTED" : "DISCONNECTED";
        var fee = await db.OtherCharges.AsNoTracking().SingleOrDefaultAsync(c => c.Id == physical.ReconnectionFeeOtherChargeId);
        var child = await db.OtherChargeInstallments.AsNoTracking().SingleOrDefaultAsync(i => i.OtherChargeId == physical.ReconnectionFeeOtherChargeId);
        if (fee == null || child == null || !fee.IsActive || fee.Amount != 350m || child.Amount != 350m
            || fee.InstallmentCount != 1 || !bills.Any(b => b.BillingReferenceId == fee.BillingReferenceId))
            throw new InvalidOperationException("The disconnection fee requires reconciliation.");
        result.ReconnectionFeeBalance = collections[fee.BillingReferenceId].CollectibleOtherCharges
            .Where(c => c.Id == fee.Id).Sum(c => c.Amount);
        result.CollectibleOtherCharges -= result.ReconnectionFeeBalance;
        var feePaid = fee.PaymentStatus == (int)PaymentStatus.Paid && child.PaymentStatus == (int)PaymentStatus.Paid
            && child.SettledPaymentId.HasValue;
        if (!physical.ActualReconnectionDateUtc.HasValue) {
            result.Status = result.TotalCollectibleBalance == 0m && feePaid
                ? "ELIGIBLE FOR RECONNECTION" : "BALANCE + RECONNECTION FEE PENDING";
        } else {
            var ids = System.Text.Json.JsonSerializer.Deserialize<List<long>>(physical.SettlementPaymentIdsJson ?? "[]") ?? [];
            result.FinancialReviewRequired = !feePaid || await db.Payments.AnyAsync(p => ids.Contains(p.Id) && !p.Success);
            if (result.FinancialReviewRequired) result.Status = "FINANCIAL / SERVICE STATE REVIEW REQUIRED";
            else if (second == null || second.BillingPeriod.Date <= physical.SubjectDate) result.Status = "RECONNECTED";
        }
        return result;
    }

    public async Task<ServiceDisconnectionEvent> ConfirmDisconnection(string accountNumber, Guid operationId, int actorId, DateTime utcNow, DateOnly today)
    {
        ValidateConfirmation(accountNumber, operationId, actorId, utcNow);
        var accountId = await FindAccountId(accountNumber);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var account = await LockAccountAndBills(accountId, accountNumber);
        var id = operationId.ToString("D");
        var replay = await db.ServiceDisconnectionEvents.SingleOrDefaultAsync(e => e.Id == id);
        if (replay != null) {
            if (replay.AccountId != account.Id || replay.DisconnectedBy != actorId) throw new InvalidOperationException("Confirmation identity conflict.");
            await transaction.CommitAsync(); return replay;
        }
        if (await db.ServiceDisconnectionEvents.AnyAsync(e => e.ReconnectionOperationId == id))
            throw new InvalidOperationException("Confirmation identity conflict.");
        var state = await Evaluate(account, today);
        if (state.Status != "FOR DISCONNECTION" || await db.ServiceDisconnectionEvents.AnyAsync(e => e.AccountId == account.Id && e.ActualReconnectionDateUtc == null))
            throw new InvalidOperationException("The account is not eligible for physical disconnection.");
        var first = await db.Billings.SingleAsync(b => b.BillingReferenceId == state.FirstBillingReference);
        var second = await db.Billings.SingleAsync(b => b.BillingReferenceId == state.SecondBillingReference);
        var fee = await new OtherChargeService(db).CreateReconnectionFee(second, actorId, utcNow);
        var physical = new ServiceDisconnectionEvent { Id = id, AccountId = account.Id,
            FirstBillingId = first.Id, SecondBillingId = second.Id, FirstBillingReference = first.BillingReferenceId,
            SecondBillingReference = second.BillingReferenceId, SubjectDate = state.SubjectDate!.Value,
            ScheduledDisconnectionDate = state.ScheduledDisconnectionDate!.Value,
            ActualDisconnectionDateUtc = utcNow, DisconnectedBy = actorId, ReconnectionFeeOtherChargeId = fee.Id };
        db.ServiceDisconnectionEvents.Add(physical);
        account.Status = (int)AccountStatus.Suspended;
        account.UpdatedBy = actorId; account.DateUpdated = utcNow;
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        return physical;
    }

    public async Task<ServiceDisconnectionEvent> ConfirmReconnection(string accountNumber, Guid operationId, int actorId, DateTime utcNow, DateOnly today)
    {
        ValidateConfirmation(accountNumber, operationId, actorId, utcNow);
        var accountId = await FindAccountId(accountNumber);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var account = await LockAccountAndBills(accountId, accountNumber);
        var id = operationId.ToString("D");
        var replay = await db.ServiceDisconnectionEvents.SingleOrDefaultAsync(e => e.ReconnectionOperationId == id);
        if (replay != null) {
            if (replay.AccountId != account.Id || replay.ReconnectedBy != actorId) throw new InvalidOperationException("Confirmation identity conflict.");
            await transaction.CommitAsync(); return replay;
        }
        if (await db.ServiceDisconnectionEvents.AnyAsync(e => e.Id == id)) throw new InvalidOperationException("Confirmation identity conflict.");
        var state = await Evaluate(account, today);
        if (state.Status != "ELIGIBLE FOR RECONNECTION" || account.Status != (int)AccountStatus.Suspended)
            throw new InvalidOperationException("All collectible obligations and the reconnection fee must be settled, and the account must be Suspended.");
        var physical = (await db.ServiceDisconnectionEvents.FromSqlInterpolated(
            $"SELECT * FROM service_disconnection_events WHERE Id = {state.EventId} FOR UPDATE").ToListAsync()).Single();
        if (physical.AccountId != account.Id || physical.ActualDisconnectionDateUtc == default || physical.ActualReconnectionDateUtc != null)
            throw new InvalidOperationException("An active confirmed disconnection event is required.");
        physical.ReconnectionEligibleDateUtc = utcNow; physical.ActualReconnectionDateUtc = utcNow;
        physical.ReconnectedBy = actorId; physical.ReconnectionOperationId = id;
        account.Status = (int)AccountStatus.Active;
        account.UpdatedBy = actorId; account.DateUpdated = utcNow;
        var references = await db.Billings.Where(b => b.AccountId == account.Id).Select(b => b.BillingReferenceId).ToListAsync();
        physical.SettlementPaymentIdsJson = System.Text.Json.JsonSerializer.Serialize(await db.Payments.AsNoTracking()
            .Where(p => references.Contains(p.BillingReference) && p.Success).Select(p => p.Id).OrderBy(id => id).ToListAsync());
        await db.SaveChangesAsync(); await transaction.CommitAsync(); return physical;
    }

    async Task<long> FindAccountId(string number) => await db.Accounts.AsNoTracking().Where(a => a.AccountNumber == number.Trim())
        .Select(a => (long?)a.Id).SingleOrDefaultAsync() ?? throw new KeyNotFoundException("Account not found.");

    internal static Task<ServiceDisconnectionEvent?> GetLatestPhysicalEvent(UserDbContext context, long accountId) =>
        context.ServiceDisconnectionEvents.AsNoTracking().Where(e => e.AccountId == accountId)
            .OrderByDescending(e => e.ActualDisconnectionDateUtc).ThenByDescending(e => e.Id).FirstOrDefaultAsync();

    async Task<Account> LockAccountAndBills(long accountId, string number)
    {
        // Same bill-first lock order as payment/cancellation/adjustment; range lock also protects new bills.
        // Resolve identity before beginning the transaction: a pre-lock consistent read would freeze stale state.
        await db.Billings.FromSqlInterpolated($"SELECT * FROM `Billings` WHERE AccountId = {accountId} ORDER BY BillingReferenceId FOR UPDATE").ToListAsync();
        var account = (await db.Accounts.FromSqlInterpolated($"SELECT * FROM accounts WHERE Id = {accountId} FOR UPDATE").ToListAsync()).SingleOrDefault()
            ?? throw new KeyNotFoundException("Account not found.");
        if (account.AccountNumber != number.Trim()) throw new InvalidOperationException("Account identity changed. Reload monitoring.");
        return account;
    }
    static void ValidateAccount(string number) {
        if (string.IsNullOrWhiteSpace(number) || number.Trim().Length > 50) throw new ArgumentException("A valid account number is required.");
    }
    static void ValidateConfirmation(string number, Guid operationId, int actorId, DateTime utcNow) {
        ValidateAccount(number);
        if (operationId == Guid.Empty || actorId <= 0 || utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A valid operation identity, actor and UTC timestamp are required.");
    }
}
