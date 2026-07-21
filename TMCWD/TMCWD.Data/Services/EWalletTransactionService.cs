using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class EWalletTransactionService : IEWalletTransactionService
    {

        #region fields

        private readonly UserDbContext _context;

        #endregion

        #region constructors

        public EWalletTransactionService(UserDbContext context)
        {
            _context = context;
        }

        #endregion

        #region methods

        public async Task<EWalletTransaction> Get(int id)
        {
            var eWalletTransaction = await _context.EWalletTransactions.Where(x => x.Id == id).FirstOrDefaultAsync();
            return eWalletTransaction;
        }

        public async Task<List<EWalletTransaction>> GetByPaymentReference(string reference)
        {
            var eWalletTransactions = await _context.EWalletTransactions.Where(x => x.PaymentReference.ToLower() == reference.ToLower()).ToListAsync();
            return eWalletTransactions;
        }

        public async Task<List<EWalletTransaction>> GetByProcessedBy(int userId)
        {
            var eWalletTransactions = await _context.EWalletTransactions.Where(x => x.CreatedBy == userId).ToListAsync();
            return eWalletTransactions;
        }

        public async Task<EWalletTransaction> SaveUpdate(int userId, EWalletTransaction transaction)
        {
            if(transaction.Id == 0)
            {
                transaction.CreatedBy = userId;
                transaction.DateCreated = DateTime.Now;
                _context.EWalletTransactions.Add(transaction);
            }
            else
            {
                var eWalletTrans = await _context.EWalletTransactions.Where(x => x.Id == transaction.Id).FirstOrDefaultAsync();
                if(eWalletTrans != null)
                {
                    eWalletTrans.UpdatedBy = userId;
                    eWalletTrans.DateUpdated = DateTime.Now;
                    eWalletTrans.Data = transaction.Data;
                    _context.EWalletTransactions.Update(eWalletTrans);

                    transaction = eWalletTrans;
                }
            }

            await _context.SaveChangesAsync();

            return transaction;
        }

        #endregion

    }
}
