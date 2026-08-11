using Microsoft.EntityFrameworkCore;
using System.Security.Principal;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class AccountService : IAccountService
    {

        #region fields

        private readonly UserDbContext _dbContext;

        #endregion

        #region ctor

        public AccountService(UserDbContext context)
        {
            _dbContext = context;
        }

        #endregion

        #region methods

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

        public async Task<IEnumerable<Account>> GetByZoneAndBook(int zone, int book)
        {
            //var accounts = _dbContext.Accounts.Where(x => x.Zone == zone && x.Book == book);
            var accts = from zoneBooks in _dbContext.ZoneBooks
                        join accounts in _dbContext.Accounts on zoneBooks.Id equals accounts.ZoneBookId
                        where zoneBooks.Zone == zone && zoneBooks.Book == book
                        select accounts;
            return await accts.ToListAsync();
        }

        public async Task<IEnumerable<Account>> GetByZoneBookId(int zoneBookId)
        {
            var accounts = await _dbContext.Accounts.Where(x => x.ZoneBookId == zoneBookId).ToListAsync();
            return accounts;
        }

        public async Task<IEnumerable<Account>> GetByZoneBookAndSequence(int zone, int book, int seqFrom, int seqTo)
        {
            var data = from zoneBooks in _dbContext.ZoneBooks
                       join accounts in _dbContext.Accounts on zoneBooks.Id equals accounts.ZoneBookId
                       where zoneBooks.Zone == zone && zoneBooks.Book == book && 
                       (accounts.Sequence >= seqFrom || seqFrom == 0) &&
                       (accounts.Sequence <= seqTo || seqTo == 0)
                       select accounts;
            return await data.ToListAsync();
        }

        public async Task<IEnumerable<Account>> Search(string query)
        {
            var q = query.Trim().ToLower();
            var accounts = await _dbContext.Accounts
                .Where(x => x.AccountNumber.ToLower().Contains(q)
                          || x.MeterNumber.ToLower().Contains(q)
                          || (x.HouseNumber != null && x.HouseNumber.ToLower().Contains(q))
                          || (x.Street != null && x.Street.ToLower().Contains(q))
                          || (x.Barangay != null && x.Barangay.ToLower().Contains(q)))
                .Take(30)
                .ToListAsync();
            return accounts;
        }

        #endregion
    }
}
