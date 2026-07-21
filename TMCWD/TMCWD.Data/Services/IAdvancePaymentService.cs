using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IAdvancePaymentService
    {
        public Task<AdvancePayment> Get(long id);

        public Task<List<AdvancePayment>> GetAll();

        public Task<AdvancePayment> GetActiveByAccount(int accountId);

        public Task<List<AdvancePayment>> GetByAccount(int accountId);

        public Task<AdvancePayment> SaveUpdate(int userId, AdvancePayment advancePayment);
    }
}
