using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IBillingAdjustmentService
    {

        public Task<BillingAdjustment> Get(int id);

        public Task<List<BillingAdjustment>> GetAll();

        public Task<List<BillingAdjustment>> GetByReference(string reference);

        public Task<BillingAdjustment> SaveUpdate(int userId, BillingAdjustment billingAdjustment);

    }
}
