using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IEWalletTransactionService
    {

        public Task<EWalletTransaction> Get(int id);

        public Task<List<EWalletTransaction>> GetByPaymentReference(string reference);

        public Task<List<EWalletTransaction>> GetByProcessedBy(int userId);

        public Task<EWalletTransaction> SaveUpdate(int userId, EWalletTransaction transaction);

    }
}
