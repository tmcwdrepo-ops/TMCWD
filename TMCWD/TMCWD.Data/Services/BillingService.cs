using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class BillingService : IBillingService
    {

        private readonly UserDbContext _dbContext;

        public BillingService(UserDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Billing> Get(int id)
        {
            var billing = await _dbContext.Billings.Where(x => x.Id == id).FirstOrDefaultAsync();
            return billing;
        }

        public async Task<List<Billing>> GetAll()
        {
            var billings = _dbContext.Billings;
            return await billings.ToListAsync();
        }

        public async Task<List<Billing>> GetByAccountId(int accountId)
        {
            var billings = _dbContext.Billings.Where(x => x.AccountId == accountId);
            return await billings.ToListAsync();
        }

        public async Task<Billing> GetByBillingReference(string billingReferenceId)
        {
            var billing = await _dbContext.Billings.Where(x => x.BillingReferenceId == billingReferenceId).FirstOrDefaultAsync();
            return billing;
        }

        public async Task<TMCWD.Model.Billing.CollectionBill?> GetCollectionBill(string billingReferenceId)
        {
            var bill = await _dbContext.Billings.AsNoTracking()
                .SingleOrDefaultAsync(b => b.BillingReferenceId == billingReferenceId);
            if (bill == null) return null;
            var invoice = bill.InvoiceSnapshotJson == null ? null
                : System.Text.Json.JsonSerializer.Deserialize<TMCWD.Model.Billing.OfficialInvoiceSnapshot>(bill.InvoiceSnapshotJson);
            var sheet = await (from reading in _dbContext.Readings.AsNoTracking()
                               join s in _dbContext.ReadingSheets.AsNoTracking() on reading.ReadingSheetId equals s.Id
                               where reading.Id == bill.ReadingId select s).SingleOrDefaultAsync();
            var charges = await _dbContext.OtherCharges.AsNoTracking()
                .Where(c => c.BillingReferenceId == billingReferenceId && c.IsActive
                    && (c.PaymentStatus == (int)PaymentStatus.Unpaid || c.PaymentStatus == (int)PaymentStatus.Overdue))
                .SumAsync(c => (decimal?)c.Amount) ?? 0m;
            var penalty = await _dbContext.Penalties.AsNoTracking()
                .Where(p => p.BillingReferenceId == billingReferenceId
                    && (p.PaymentStatus == (int)PaymentStatus.Unpaid || p.PaymentStatus == (int)PaymentStatus.Overdue))
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;
            var settled = bill.PaymentStatus == (int)PaymentStatus.Paid || bill.PaymentStatus == (int)PaymentStatus.Waived;
            // Preserve legacy connection-stub recovery; never infer a water-charge component.
            var remaining = settled ? 0m : Math.Max(0m, Math.Max(bill.RemainingAmount, charges));
            var futurePenalty = invoice == null ? (decimal?)null : TMCWD.Model.Billing.LatePaymentPenalty.Calculate(invoice.BillAmount);
            return new TMCWD.Model.Billing.CollectionBill {
                BillingReferenceId = bill.BillingReferenceId, ServiceInvoiceNumber = bill.ServiceInvoiceNumber,
                PaymentReference = string.IsNullOrWhiteSpace(bill.PaymentTransactionId) ? null : bill.PaymentTransactionId,
                BillAmount = invoice?.BillAmount, MeterMaintenanceFee = invoice?.MeterMaintenance,
                OtherCharges = charges, BillingAdjustment = bill.BillingAdjustment, RemainingAmount = remaining,
                Penalty = settled ? 0m : penalty, AmountDue = settled ? 0m : remaining + penalty,
                TotalDue = invoice?.TotalDue, PenaltyAfterDue = futurePenalty,
                TotalAfterDue = invoice == null ? null : invoice.TotalDue + futurePenalty,
                BillingDate = sheet?.BillingDate ?? bill.BillingPeriod, DueDate = sheet?.DueDate,
                DisconnectionDate = sheet?.DisconnectionDate, PeriodFrom = invoice?.PeriodFrom, PeriodTo = invoice?.PeriodTo
            };
        }

        public async Task<List<Billing>> GetByJobOrderId(int jobOrderId)
        {
            var billings = _dbContext.Billings.Where(x => x.JobOrderId == jobOrderId);
            return await billings.ToListAsync();
        }

        public async Task<Billing> GetByPaymentTransactionId(string paymentTransactionId)
        {
            var billing = await _dbContext.Billings.Where(x => x.PaymentTransactionId == paymentTransactionId).FirstOrDefaultAsync();
            return billing;
        }

        public async Task<Billing> SaveUpdate(int userId, Billing billing)
        {
            var storedSnapshot = billing.Id > 0 ? await _dbContext.Billings.AsNoTracking()
                .Where(b => b.Id == billing.Id).Select(b => b.InvoiceSnapshotJson).SingleAsync() : null;
            if (billing.InvoiceSnapshotJson != null && billing.InvoiceSnapshotJson != storedSnapshot)
                throw new InvalidOperationException("Official invoice snapshots may only be assigned by posting and are immutable.");
            billing.InvoiceSnapshotJson = storedSnapshot;
            var invoice = billing.Id > 0 ? await _dbContext.Billings.AsNoTracking()
                .Where(b => b.Id == billing.Id).Select(b => b.ServiceInvoiceNumber).SingleAsync() : null;
            if (billing.ServiceInvoiceNumber != invoice)
                throw new InvalidOperationException("Service invoice numbers may only be assigned by posting and are immutable.");
            if (billing.Id > 0)
            {
                billing.UpdatedBy = userId;
                billing.DateUpdated = DateTime.Now;
                _dbContext.Billings.Update(billing);

            }
            else
            {
                billing.CreatedBy = userId;
                billing.DateCreated = DateTime.Now;
                _dbContext.Billings.Add(billing);
            }

            await _dbContext.SaveChangesAsync();
            return billing;
        }

        public async Task<Billing> GetByReadingId(int readingId)
        {
            var billing = await _dbContext.Billings.Where(x => x.ReadingId == readingId).FirstOrDefaultAsync();
            return billing;
        }

        public async Task<List<Billing>> GetByReadings(int[] readingIds)
        {
            var ids = readingIds.ToList();
            var billings = await _dbContext.Billings.Where(x => ids.Contains((int)x.ReadingId)).ToListAsync();
            return billings;
        }

        public async Task<Billing?> GetByServiceInvoiceNumber(string serviceInvoiceNumber)
        {
            if (string.IsNullOrWhiteSpace(serviceInvoiceNumber)) return null;
            return await _dbContext.Billings
                .Where(x => x.ServiceInvoiceNumber == serviceInvoiceNumber.Trim())
                .FirstOrDefaultAsync();
        }

        public async Task<string> AssignServiceInvoiceNumber(Billing billing)
        {
            if (_dbContext.Database.CurrentTransaction == null)
                throw new InvalidOperationException("Invoice assignment requires the posting transaction.");
            if (billing.Id > 0)
            {
                var persisted = await _dbContext.Billings.AsNoTracking().SingleAsync(b => b.Id == billing.Id);
                if (persisted.ServiceInvoiceNumber != null)
                {
                    billing.ServiceInvoiceNumber = persisted.ServiceInvoiceNumber;
                    return persisted.ServiceInvoiceNumber;
                }
                throw new InvalidOperationException("Historical billings cannot be assigned invoice numbers automatically.");
            }
            if (billing.ServiceInvoiceNumber != null)
                throw new InvalidOperationException("New billings must obtain their invoice number from the configured series.");
            var configs = await _dbContext.BirInvoiceConfigs
                .FromSqlRaw("SELECT * FROM bir_invoice_configs WHERE IsActive = 1 ORDER BY Id FOR UPDATE")
                .AsNoTracking().ToListAsync();
            if (configs.Count != 1)
                throw new InvalidOperationException("Exactly one active BIR invoice configuration is required.");
            var config = configs[0];
            BirInvoiceConfigService.Validate(config);
            var date = DateOnly.FromDateTime(billing.BillingPeriod);
            if (config.EffectiveFrom > date || config.EffectiveTo < date)
                throw new InvalidOperationException("The active BIR invoice configuration does not cover this billing period.");
            // The config lock serializes first use and every subsequent allocation.
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT IGNORE INTO service_invoice_seq (Id, LastValue) VALUES ({config.Id}, {config.SeriesFrom - 1})");
            var sequence = (await _dbContext.ServiceInvoiceSeq
                .FromSqlInterpolated($"SELECT * FROM service_invoice_seq WHERE Id = {config.Id} FOR UPDATE")
                .AsNoTracking().ToListAsync()).Single();
            if (sequence.LastValue >= config.SeriesTo)
                throw new InvalidOperationException("The active BIR invoice series is exhausted. Posting was not saved.");
            var next = Math.Max(config.SeriesFrom, sequence.LastValue + 1);
            var width = config.SeriesWidth ?? config.SeriesTo.ToString(System.Globalization.CultureInfo.InvariantCulture).Length;
            var number = (config.InvoicePrefix ?? "") + next.ToString("D" + width, System.Globalization.CultureInfo.InvariantCulture);
            if (await _dbContext.Billings.AnyAsync(b => b.ServiceInvoiceNumber == number))
                throw new InvalidOperationException("The configured invoice number has already been used. Posting was not saved.");
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE service_invoice_seq SET LastValue = {next} WHERE Id = {config.Id}");
            billing.ServiceInvoiceNumber = number;
            return number;
        }

    }
}
