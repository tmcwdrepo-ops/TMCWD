using Microsoft.EntityFrameworkCore;
using System.Security.Principal;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class AccountService : IAccountService
    {

        private readonly UserDbContext _dbContext;

        public AccountService(UserDbContext context)
        {
            _dbContext = context;
        }

        public async Task<Account?> Get(int id)
        {
            var account = await _dbContext.Accounts.Where(x => x.Id == id).FirstOrDefaultAsync();
            return account;
        }

        public async Task<Account?> GetByAccountNumber(string accountNumber)
        {
            var account = await _dbContext.Accounts.Where(x => x.AccountNumber.ToLower() == accountNumber.ToLower()).FirstOrDefaultAsync();
            return account;
        }

        public async Task<IEnumerable<Account>> GetByCustomerId(int customerId)
        {
            var accounts = _dbContext.Accounts.Where(x => x.CustomerId == customerId);
            return await accounts.ToListAsync();
        }

        public async Task<Account?> GetByMeterNumber(string meterNumber)
        {
            var account = await _dbContext.Accounts.Where(x => x.MeterNumber.ToLower() == meterNumber.ToLower()).FirstOrDefaultAsync();
            return account;
        }

        public async Task<Account> SaveUpdate(int userId, Account account)
        {
            if(account.Id > 0)
            {
                account.UpdatedBy = userId;
                account.DateUpdated = DateTime.Now;
                _dbContext.Accounts.Update(account);
            }
            else
            {
                account.Status = (int)AccountStatus.Pending;
                account.CreatedBy = userId;
                account.DateCreated = DateTime.Now;
                _dbContext.Accounts.Update(account);
            }

            await _dbContext.SaveChangesAsync();

            return account;
        }

        public async Task<IEnumerable<Account>> GetAccounts()
        {
            var accounts = _dbContext.Accounts;
            return await accounts.ToListAsync();
        }

        public async Task<List<Account>> GetByZoneAndBook(int zone, int book)
        {
            //var accounts = _dbContext.Accounts.Where(x => x.Zone == zone && x.Book == book);
            var accts = from zoneBooks in _dbContext.ZoneBooks
                        join accounts in _dbContext.Accounts on zoneBooks.Id equals accounts.ZoneBookId
                        where zoneBooks.Zone == zone && zoneBooks.Book == book
                        select accounts;
            return await accts.ToListAsync();
        }
    }
}
