using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IPenaltyService
    {
        public Task<Penalty> Get(int id);

        public Task<List<Penalty>> GetAll();

        public Task<List<Penalty>> GetByReference(string referenceId);

        public Task<Penalty> SaveUpdate(int userId, Penalty penalty);

        public Task<int> WaiveUnpaid(int userId, List<string> billingReferences);

        Task<int> SubmitWaiverRequests(int userId, TMCWD.Model.Billing.SubmitPenaltyWaiverRequest request);
        Task<List<TMCWD.Model.Billing.PenaltyWaiverRequestRecord>> GetPendingWaiverRequests(int userId);
        Task<int> ReviewWaiverRequest(int userId, TMCWD.Model.Billing.ReviewPenaltyWaiverRequest request);

        Task<List<TMCWD.Model.Billing.PenaltyChargeEntry>> GetChargeEntries(DateTime? billingPeriod);
        Task<TMCWD.Model.Billing.PostPenaltyChargesResult> PostCharges(int userId, TMCWD.Model.Billing.PostPenaltyChargesRequest request);

    }
}
