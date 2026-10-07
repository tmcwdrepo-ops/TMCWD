using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class OtherChargeService : IOtherChargeService
    {
        private readonly UserDbContext _context;

        public OtherChargeService(UserDbContext context)
        {
            _context = context;
        }

        public async Task<OtherCharge> Get(int id)
        {
            var otherCharge = await _context.OtherCharges.Where(x => x.Id == id).FirstOrDefaultAsync();
            return otherCharge;
        }

        public async Task<List<OtherCharge>> GetAll()
        {
            var otherCharges = await _context.OtherCharges.ToListAsync();
            return otherCharges;
        }

        public async Task<List<OtherCharge>> GetByReference(string referenceId)
        {
            var otherCharges = await _context.OtherCharges
                .Where(x => x.BillingReferenceId == referenceId && x.IsActive)
                .ToListAsync();
            return otherCharges;
        }

        public async Task<OtherCharge> SaveUpdate(int userId, OtherCharge otherCharge)
        {
            if(otherCharge.Id > 0)
            {
                otherCharge.UpdatedBy = userId;
                otherCharge.DateUpdated = DateTime.Now;
                _context.OtherCharges.Update(otherCharge);
            }
            else
            {
                otherCharge.CreatedBy = userId;
                otherCharge.DateCreated = DateTime.Now;
                _context.OtherCharges.Add(otherCharge);
            }

            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(otherCharge.BillingReferenceId))
                await UpdateBillingOtherCharges(otherCharge.BillingReferenceId, userId);

            return otherCharge;
        }

        private async Task UpdateBillingOtherCharges(string billingReferenceId, int userId)
        {
            var billing = await _context.Billings
                .FirstOrDefaultAsync(x => x.BillingReferenceId == billingReferenceId);
            if (billing == null) return;

            var postedTotal = await _context.OtherCharges
                .Where(x => x.BillingReferenceId == billingReferenceId && x.IsActive
                    && (x.PaymentStatus == (int)PaymentStatus.Unpaid
                     || x.PaymentStatus == (int)PaymentStatus.Overdue))
                .SumAsync(x => (decimal?)x.Amount) ?? 0m;
            var difference = postedTotal - billing.OtherCharges;

            billing.OtherCharges = postedTotal;
            billing.TotalBillAmount = Math.Max(postedTotal, billing.TotalBillAmount + difference);
            billing.RemainingAmount = Math.Max(postedTotal, billing.RemainingAmount + difference);
            billing.UpdatedBy = userId;
            billing.DateUpdated = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        public async Task<int> PostToCollection(int userId, List<int> otherChargeIds)
        {
            if (userId <= 0)
                throw new InvalidOperationException("A valid user is required.");
            if (otherChargeIds == null || otherChargeIds.Count == 0 || otherChargeIds.Count > 1000)
                throw new InvalidOperationException("Select at least one draft charge to post.");

            var ids = otherChargeIds.Distinct().OrderBy(x => x).ToList();
            if (ids.Any(x => x <= 0))
                throw new InvalidOperationException("The selected charges are invalid.");

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var charges = new List<OtherCharge>();
            foreach (var id in ids)
            {
                charges.AddRange(await _context.OtherCharges
                    .FromSqlInterpolated($"SELECT * FROM `other_charges` WHERE Id = {id} FOR UPDATE")
                    .ToListAsync());
            }

            if (charges.Count != ids.Count || charges.Any(x => !x.IsActive))
                throw new InvalidOperationException("One or more selected charges are no longer available.");
            if (charges.Any(x => x.PaymentStatus != (int)PaymentStatus.Draft
                              && x.PaymentStatus != (int)PaymentStatus.Unpaid
                              && x.PaymentStatus != (int)PaymentStatus.Overdue))
                throw new InvalidOperationException("One or more selected charges cannot be posted.");

            var references = charges.Select(x => x.BillingReferenceId)
                .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            if (references.Count == 0 || charges.Any(x => string.IsNullOrWhiteSpace(x.BillingReferenceId)))
                throw new InvalidOperationException("A selected charge is not attached to a billing record.");
            var billings = await _context.Billings
                .Where(x => references.Contains(x.BillingReferenceId)).ToListAsync();
            if (billings.Count != references.Count
                || billings.Any(x => x.PaymentStatus == (int)PaymentStatus.Paid
                                  || x.PaymentStatus == (int)PaymentStatus.Waived))
                throw new InvalidOperationException("The billing record for a selected charge is already settled.");

            var now = DateTime.Now;
            foreach (var charge in charges.Where(x => x.PaymentStatus == (int)PaymentStatus.Draft))
            {
                charge.PaymentStatus = (int)PaymentStatus.Unpaid;
                charge.UpdatedBy = userId;
                charge.DateUpdated = now;
            }
            await _context.SaveChangesAsync();

            foreach (var reference in references)
                await UpdateBillingOtherCharges(reference, userId);

            // Posting the connection fee completes the Charging queue step.
            // Keep this in the same transaction as the ledger update so the
            // application cannot be shown in Payment without its charge.
            var accountIds = billings.Select(x => x.AccountId).Distinct().ToList();
            var applications = await _context.WaterConnectionApplications
                .Where(x => x.AccountId.HasValue
                    && accountIds.Contains(x.AccountId.Value)
                    && x.Stage == "charging")
                .ToListAsync();
            foreach (var application in applications)
            {
                var billing = billings.First(x => x.AccountId == application.AccountId);
                application.ChargingData = JsonSerializer.Serialize(new
                {
                    reference = billing.BillingReferenceId,
                    amount = billing.OtherCharges
                });
                application.Stage = "payment";
                application.UpdatedAt = now;
            }
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return charges.Count;
        }

        public async Task<OtherCharge> Deactivate(int id, int userId)
        {
            var otherCharge = await _context.OtherCharges.Where(x => x.Id == id).FirstOrDefaultAsync();
            if (otherCharge == null) return null;
            if (otherCharge.PaymentStatus != (int)PaymentStatus.Draft)
                throw new InvalidOperationException("Posted charges cannot be removed from Collection.");

            otherCharge.IsActive = false;
            otherCharge.UpdatedBy = userId;
            otherCharge.DateUpdated = DateTime.Now;

            _context.OtherCharges.Update(otherCharge);
            await _context.SaveChangesAsync();
            if (!string.IsNullOrWhiteSpace(otherCharge.BillingReferenceId))
                await UpdateBillingOtherCharges(otherCharge.BillingReferenceId, userId);
            return otherCharge;
        }

        public async Task<string> ProcessPayment(
            int userId,
            TMCWD.Model.Billing.Requests.OtherChargePaymentRequest request)
        {
            if (userId <= 0)
                throw new InvalidOperationException("A valid cashier is required.");
            if (string.IsNullOrWhiteSpace(request.InvoiceNumber))
                throw new InvalidOperationException("Invoice number is required.");
            if (!Enum.IsDefined(typeof(PaymentMethod), request.Method))
                throw new InvalidOperationException("A valid payment method is required.");
            if (request.OtherChargeIds == null || request.OtherChargeIds.Count == 0)
                throw new InvalidOperationException("Select at least one charge to pay.");

            var invoiceNumber = request.InvoiceNumber.Trim().ToUpperInvariant();
            await using var referenceLock = await PaymentReferenceLock.Acquire(_context, invoiceNumber);
            if (await _context.Payments.AnyAsync(x => x.PaymentReference == invoiceNumber))
                throw new InvalidOperationException("This invoice number has already been used.");

            // Lock and load the charge rows atomically.
            var ids = request.OtherChargeIds.Distinct().OrderBy(x => x).ToList();
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var charges = new List<OtherCharge>();
            foreach (var id in ids)
            {
                var rows = await _context.OtherCharges
                    .FromSqlInterpolated($"SELECT * FROM `other_charges` WHERE Id = {id} FOR UPDATE")
                    .ToListAsync();
                charges.AddRange(rows);
            }

            if (charges.Count != ids.Count)
                throw new InvalidOperationException("One or more selected charges no longer exist.");

            var inactive = charges.Where(c => !c.IsActive).ToList();
            if (inactive.Count > 0)
                throw new InvalidOperationException("One or more selected charges are no longer active.");

            var notCollectible = charges.Where(c => c.PaymentStatus != (int)PaymentStatus.Unpaid
                                                 && c.PaymentStatus != (int)PaymentStatus.Overdue).ToList();
            if (notCollectible.Count > 0)
                throw new InvalidOperationException("One or more selected charges have not been posted or were already paid.");

            var totalDue = charges.Sum(c => c.Amount);
            if (request.AmountTendered < totalDue)
                throw new InvalidOperationException("Amount tendered is less than the total due.");

            var now = DateTime.Now;
            var payments = new List<Payment>();

            var billingReferences = charges
                .Select(x => x.BillingReferenceId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var lockedBillings = new List<Billing>();
            foreach (var reference in billingReferences)
            {
                lockedBillings.AddRange(await _context.Billings
                    .FromSqlInterpolated($"SELECT * FROM `Billings` WHERE BillingReferenceId = {reference} FOR UPDATE")
                    .ToListAsync());
            }

            foreach (var charge in charges)
            {
                charge.PaymentStatus = (int)PaymentStatus.Paid;
                charge.UpdatedBy = userId;
                charge.DateUpdated = now;

                payments.Add(new Payment
                {
                    // Re-use BillingReference as the natural FK anchor;
                    // for standalone other-charges (no billing ref) we write a synthetic key.
                    BillingReference = string.IsNullOrWhiteSpace(charge.BillingReferenceId)
                        ? $"OC-{charge.Id}"
                        : charge.BillingReferenceId,
                    PaymentReference = invoiceNumber,
                    MacAddress = "WEB",
                    PaidAmount = charge.Amount,
                    Method = (int)request.Method,
                    Success = true,
                    CreatedBy = userId,
                    DateCreated = now
                });
            }

            _context.OtherCharges.UpdateRange(charges);
            _context.Payments.AddRange(payments);
            await _context.SaveChangesAsync();

            // Remove paid charges from their billing balance. When the charge was
            // the billing's only collectible item (new-connection stubs), close
            // that billing so it no longer appears as an empty unpaid row.
            foreach (var reference in billingReferences)
                await UpdateBillingOtherCharges(reference, userId);

            // Re-fetch billings after the rollup so RemainingAmount/OtherCharges
            // reflect the updates made by UpdateBillingOtherCharges (lockedBillings
            // were loaded before that call and would still hold the stale values).
            var refreshedBillings = await _context.Billings
                .Where(x => billingReferences.Contains(x.BillingReferenceId))
                .ToListAsync();

            var referencesWithPenalty = await _context.Penalties
                .Where(x => billingReferences.Contains(x.BillingReferenceId)
                    && (x.PaymentStatus == (int)PaymentStatus.Unpaid
                     || x.PaymentStatus == (int)PaymentStatus.Overdue))
                .Select(x => x.BillingReferenceId)
                .Distinct()
                .ToListAsync();
            var penaltySet = referencesWithPenalty.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var settledBillings = refreshedBillings
                .Where(x => x.RemainingAmount <= 0m && x.OtherCharges <= 0m
                    && !penaltySet.Contains(x.BillingReferenceId))
                .ToList();
            foreach (var billing in settledBillings)
            {
                billing.PaymentStatus = (int)PaymentStatus.Paid;
                billing.PaymentTransactionId = invoiceNumber;
                billing.UpdatedBy = userId;
                billing.DateUpdated = now;
            }

            var settledAccountIds = settledBillings.Select(x => x.AccountId).Distinct().ToList();
            if (settledAccountIds.Count > 0)
            {
                var applications = await _context.WaterConnectionApplications
                    .Where(x => x.AccountId.HasValue
                        && settledAccountIds.Contains(x.AccountId.Value)
                        && x.Stage == "payment")
                    .ToListAsync();
                foreach (var application in applications)
                {
                    application.PaymentData = JsonSerializer.Serialize(new
                    {
                        reference = invoiceNumber,
                        method = request.Method.ToString(),
                        date = now,
                        status = "Paid"
                    });
                    application.Stage = "installation";
                    application.UpdatedAt = now;
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return invoiceNumber;
        }
    }
}
