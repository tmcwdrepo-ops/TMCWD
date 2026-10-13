using TMCWD.Data.Entities;
// Do NOT add "using TMCWD.Model.Billing;" — it creates BillingAdjustment ambiguity.
// TMCWD.Model types are fully qualified below.

namespace TMCWD.Data.Services
{
    public interface IBillingAdjustmentService
    {
        public Task<BillingAdjustment> Get(int id);

        public Task<List<BillingAdjustment>> GetAll();

        public Task<List<BillingAdjustment>> GetByReference(string reference);

        public Task<BillingAdjustment> SaveUpdate(int userId, BillingAdjustment billingAdjustment, Guid operationId);

        public Task<List<BillingAdjustment>> SaveBatch(int userId, List<BillingAdjustment> billingAdjustments, Guid operationId);

        /// <summary>
        /// Returns the full audit history for a billing reference, newest first,
        /// with resolved actor names. Never mutates financial state.
        /// Returns an empty list when no adjustments exist for the reference.
        /// </summary>
        public Task<List<TMCWD.Model.Billing.BillingAdjustmentHistoryDto>> GetHistory(string billingReferenceId);
    }
}
