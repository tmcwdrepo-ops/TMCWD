using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography.X509Certificates;
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

        public async Task<List<Billing>> GetByReadingId(int readingId)
        {
            var billings = await _dbContext.Billings.Where(x => x.ReadingId == readingId).ToListAsync();
            return billings;
        }

        public async Task<List<Billing>> GetByReadings(int[] readingIds)
        {
            var readingList = readingIds.ToList();
            var billings = await _dbContext.Billings.Where(x => readingList.Contains((int)x.Id)).ToListAsync();
            return billings;
        }

        public async Task<Billing> SaveUpdate(int userId, Billing billing)
        {
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

        Task<Billing> IBillingService.GetByReadingId(int readingId)
        {
            throw new NotImplementedException();
        }
    }
}
