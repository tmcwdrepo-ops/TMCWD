using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MySqlConnector;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
// Do NOT add "using TMCWD.Model.Billing;" here — it would make BillingAdjustment ambiguous
// between the entity and the DTO.  Use fully-qualified names for TMCWD.Model types below.

namespace TMCWD.Data.Services
{
    public class BillingAdjustmentService : IBillingAdjustmentService
    {
        private readonly UserDbContext _context;

        public BillingAdjustmentService(UserDbContext context)
        {
            _context = context;
        }

        public async Task<BillingAdjustment> Get(int id)
        {
            var billingAdjustment = await _context.BillingAdjustments.Where(x => x.Id == id).FirstOrDefaultAsync();
            return billingAdjustment;
        }

        public async Task<List<BillingAdjustment>> GetAll()
        {
            var billingAdjustments = await _context.BillingAdjustments.ToListAsync();
            return billingAdjustments;
        }

        public async Task<List<BillingAdjustment>> GetByReference(string reference)
        {
            var billingAdjustments = await _context.BillingAdjustments.Where(x => x.BillingReferenceId == reference).ToListAsync();
            return billingAdjustments;
        }

        /// <summary>
        /// Returns the full adjustment audit history for a billing reference,
        /// newest first, with resolved actor names. Read-only; no financial mutation.
        /// </summary>
        public async Task<List<TMCWD.Model.Billing.BillingAdjustmentHistoryDto>> GetHistory(string billingReferenceId)
        {
            if (string.IsNullOrWhiteSpace(billingReferenceId))
                return new List<TMCWD.Model.Billing.BillingAdjustmentHistoryDto>();

            var reference = billingReferenceId.Trim();

            // Step 1: load adjustments with billing's ServiceInvoiceNumber.
            // MySQL prohibits referencing the same temporary table (users) twice in
            // one query.  Resolve user names separately in step 2.
            var rows = await (
                from adj in _context.BillingAdjustments.AsNoTracking()
                where adj.BillingReferenceId == reference
                join bill in _context.Billings.AsNoTracking()
                    on adj.BillingReferenceId equals bill.BillingReferenceId into bills
                from bill in bills.DefaultIfEmpty()
                orderby adj.DateCreated descending, adj.Id descending
                select new
                {
                    adj.Id,
                    adj.BillingReferenceId,
                    ServiceInvoiceNumber = bill == null ? null : bill.ServiceInvoiceNumber,
                    adj.Type,
                    adj.Amount,
                    adj.Reason,
                    adj.CreatedBy,
                    adj.DateCreated,
                    adj.UpdatedBy,
                    adj.DateUpdated
                }
            ).ToListAsync();

            if (rows.Count == 0)
                return new List<TMCWD.Model.Billing.BillingAdjustmentHistoryDto>();

            // Step 2: batch-resolve all distinct actor IDs to names in one query.
            var actorIds = rows.SelectMany(r => new[] { r.CreatedBy, r.UpdatedBy })
                               .Distinct().ToList();
            var names = await _context.Users.AsNoTracking()
                .Where(u => actorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Name })
                .ToDictionaryAsync(u => u.Id, u => u.Name);

            return rows.Select(r => new TMCWD.Model.Billing.BillingAdjustmentHistoryDto
            {
                Id                  = r.Id,
                BillingReferenceId  = r.BillingReferenceId,
                ServiceInvoiceNumber = r.ServiceInvoiceNumber,
                // Resolve the type integer to the enum name for display;
                // fall back to the raw integer string if the value is unknown.
                AdjustmentType = Enum.IsDefined(typeof(AdjustmentLineType), (int)r.Type)
                    ? ((AdjustmentLineType)(int)r.Type).ToString()
                    : r.Type.ToString(CultureInfo.InvariantCulture),
                Amount              = r.Amount,
                Reason              = r.Reason,
                CreatedBy           = r.CreatedBy,
                CreatedByName       = names.GetValueOrDefault(r.CreatedBy),
                DateCreated         = r.DateCreated,
                UpdatedBy           = r.UpdatedBy,
                UpdatedByName       = names.GetValueOrDefault(r.UpdatedBy),
                DateUpdated         = r.DateUpdated
            }).ToList();
        }

        public async Task<BillingAdjustment> SaveUpdate(int userId, BillingAdjustment adjustment, Guid operationId) =>
            (await Save(userId, [adjustment], operationId, false)).Single();

        public Task<List<BillingAdjustment>> SaveBatch(int userId, List<BillingAdjustment> adjustments, Guid operationId) =>
            Save(userId, adjustments, operationId, true);

        private async Task<List<BillingAdjustment>> Save(int userId, List<BillingAdjustment> lines, Guid operationId, bool batch)
        {
            if (userId <= 0 || operationId == Guid.Empty || lines == null || lines.Count == 0)
                throw new InvalidOperationException("An explicit operation ID and at least one adjustment are required.");
            var reference = lines[0].BillingReferenceId?.Trim();
            if (string.IsNullOrWhiteSpace(reference) || reference.Length > 50 || lines.Any(line =>
                !string.Equals(line.BillingReferenceId?.Trim(), reference, StringComparison.OrdinalIgnoreCase)
                || line.Id < 0 || (batch && line.Id != 0) || line.Amount == 0
                || line.Amount > 9999.99m || line.Amount < -9999.99m || decimal.Round(line.Amount, 2) != line.Amount
                || line.Type < int.MinValue || line.Type > int.MaxValue
                || !Enum.IsDefined(typeof(AdjustmentLineType), (int)line.Type)))
                throw new InvalidOperationException("Adjustments must reference one bill, use a known type and a nonzero DECIMAL(6,2) amount.");

            // Normalise reason: trim and cap; null and whitespace-only are stored as null.
            // Reason is NOT part of the idempotency hash — callers may omit it on retry
            // without generating a new operation key. The first successful write's reason
            // is authoritative; a replay returns the original without overwriting it.
            static string? NormaliseReason(string? r)
                => string.IsNullOrWhiteSpace(r) ? null : r.Trim().Length > 255 ? r.Trim()[..255] : r.Trim();

            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
                batch, lines = lines.Select(line => new { line.Id, line.Type, Amount = line.Amount.ToString("F2", CultureInfo.InvariantCulture) }) }))));
            var key = operationId.ToString("D");
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var billing = await _context.Billings.FromSqlInterpolated(
                    $"SELECT * FROM `Billings` WHERE BillingReferenceId = {reference} FOR UPDATE").SingleOrDefaultAsync()
                    ?? throw new KeyNotFoundException("The selected billing record no longer exists.");
                var receipt = await _context.BillingAdjustmentOperations.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == key);
                if (receipt != null)
                {
                    if (receipt.ActorId != userId || receipt.PayloadHash != hash
                        || !string.Equals(receipt.BillingReferenceId, billing.BillingReferenceId, StringComparison.OrdinalIgnoreCase))
                        throw new BillingAdjustmentConflictException("The operation ID was already used for a different submission.");
                    var original = JsonSerializer.Deserialize<List<BillingAdjustment>>(receipt.ResultJson)
                        ?? throw new InvalidOperationException("The stored adjustment receipt is invalid.");
                    await transaction.CommitAsync();
                    return original;
                }
                if (billing.PaymentStatus == (int)PaymentStatus.Paid || billing.PaymentStatus == (int)PaymentStatus.Waived)
                    throw new InvalidOperationException("A settled bill cannot be adjusted.");

                var now = DateTime.UtcNow;
                var saved = new List<BillingAdjustment>();
                foreach (var line in lines)
                {
                    BillingAdjustment item;
                    if (line.Id > 0)
                    {
                        item = await _context.BillingAdjustments.SingleOrDefaultAsync(x => x.Id == line.Id)
                            ?? throw new KeyNotFoundException("The selected adjustment no longer exists.");
                        if (!string.Equals(item.BillingReferenceId, billing.BillingReferenceId, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("An adjustment cannot be moved to another bill.");
                        // On update: reason may be supplied; a null/empty reason leaves the existing value unchanged.
                        var newReason = NormaliseReason(line.Reason);
                        if (newReason != null) item.Reason = newReason;
                    }
                    else
                    {
                        item = new BillingAdjustment
                        {
                            BillingReferenceId = billing.BillingReferenceId,
                            Reason = NormaliseReason(line.Reason),
                            CreatedBy = userId,
                            DateCreated = now
                        };
                        _context.BillingAdjustments.Add(item);
                    }
                    item.Type = line.Type; item.Amount = line.Amount;
                    item.UpdatedBy = userId; item.DateUpdated = now;
                    saved.Add(item);
                }
                await _context.SaveChangesAsync();
                billing.BillingAdjustment = await _context.BillingAdjustments
                    .Where(x => x.BillingReferenceId == billing.BillingReferenceId).SumAsync(x => x.Amount);
                billing.RemainingAmount = Math.Max(TMCWD.Model.Billing.BillingCharges.LegacyMeterMaintenanceAdjustmentFloor,
                    billing.TotalBillAmount - billing.BillingAdjustment);
                billing.UpdatedBy = userId; billing.DateUpdated = DateTime.Now;
                _context.BillingAdjustmentOperations.Add(new BillingAdjustmentOperation { OperationId = key,
                    BillingReferenceId = billing.BillingReferenceId, ActorId = userId, PayloadHash = hash,
                    ResultJson = JsonSerializer.Serialize(saved), DateCreated = now });
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return saved;
            }
            catch (DbUpdateException error) when (error.InnerException is MySqlException { Number: 1062 })
            {
                await transaction.RollbackAsync();
                throw new BillingAdjustmentConflictException("The operation ID was already claimed; no adjustment was applied.");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
