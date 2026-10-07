using Microsoft.EntityFrameworkCore;
using System.Security.Principal;
using System.Text.Json;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
using TMCWD.Model.Dashboard;

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
            if (account != null) await PopulateInitialReadings(new[] { account });
            return account;
        }

        public async Task<Account?> GetByAccountNumber(string accountNumber)
        {
            var account = await _dbContext.Accounts.Where(x => x.AccountNumber.ToLower() == accountNumber.ToLower()).FirstOrDefaultAsync();
            if (account != null) await PopulateInitialReadings(new[] { account });
            return account;
        }

        public async Task<IEnumerable<Account>> GetByCustomerId(int customerId)
        {
            var accounts = _dbContext.Accounts.Where(x => x.CustomerId == customerId);
            var result = await accounts.ToListAsync();
            await PopulateInitialReadings(result);
            return result;
        }

        public async Task<Account?> GetByMeterNumber(string meterNumber)
        {
            var account = await _dbContext.Accounts.Where(x => x.MeterNumber.ToLower() == meterNumber.ToLower()).FirstOrDefaultAsync();
            if (account != null) await PopulateInitialReadings(new[] { account });
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
            var result = await accounts.ToListAsync();
            await PopulateInitialReadings(result);
            return result;
        }

        public async Task<IEnumerable<Account>> GetByZoneAndBook(int zone, int book)
        {
            //var accounts = _dbContext.Accounts.Where(x => x.Zone == zone && x.Book == book);
            var accts = from zoneBooks in _dbContext.ZoneBooks
                        join accounts in _dbContext.Accounts on zoneBooks.Id equals accounts.ZoneBookId
                        where zoneBooks.Zone == zone && zoneBooks.Book == book
                        select accounts;
            var result = await accts.ToListAsync();
            await PopulateInitialReadings(result);
            return result;
        }

        public async Task<IEnumerable<Account>> GetByZoneBookId(int zoneBookId)
        {
            var accounts = await _dbContext.Accounts.Where(x => x.ZoneBookId == zoneBookId).ToListAsync();
            await PopulateInitialReadings(accounts);
            return accounts;
        }

        public async Task<List<Account>> GetReadingSheetRoster(ReadingSheet sheet)
        {
            return (await GetByZoneBookId(sheet.ZoneBookId))
                .Where(a => a.Status == (int)AccountStatus.Active
                    && (sheet.SeqFrom.GetValueOrDefault() == 0 || a.Sequence >= sheet.SeqFrom)
                    && (sheet.SeqTo.GetValueOrDefault() == 0 || a.Sequence <= sheet.SeqTo))
                .OrderBy(a => a.Sequence).ThenBy(a => a.Id).ToList();
        }

        public async Task<IEnumerable<Account>> GetByZoneBookAndSequence(int zone, int book, int seqFrom, int seqTo)
        {
            var data = from zoneBooks in _dbContext.ZoneBooks
                       join accounts in _dbContext.Accounts on zoneBooks.Id equals accounts.ZoneBookId
                       where zoneBooks.Zone == zone && zoneBooks.Book == book && 
                       (accounts.Sequence >= seqFrom || seqFrom == 0) &&
                       (accounts.Sequence <= seqTo || seqTo == 0)
                       select accounts;
            var result = await data.ToListAsync();
            await PopulateInitialReadings(result);
            return result;
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
            await PopulateInitialReadings(accounts);
            return accounts;
        }

        private async Task PopulateInitialReadings(IEnumerable<Account> accounts)
        {
            var accountList = accounts.ToList();
            var accountIds = accountList.Select(x => x.Id).Distinct().ToList();
            if (accountIds.Count == 0) return;

            var tappingRecords = await _dbContext.WaterConnectionApplications.AsNoTracking()
                .Where(x => x.AccountId.HasValue && accountIds.Contains(x.AccountId.Value)
                    && x.TappingData != null && x.TappingData != string.Empty)
                .OrderByDescending(x => x.UpdatedAt)
                .Select(x => new { AccountId = x.AccountId!.Value, x.TappingData })
                .ToListAsync();

            var initialByAccount = new Dictionary<long, decimal>();
            foreach (var tapping in tappingRecords)
            {
                if (initialByAccount.ContainsKey(tapping.AccountId)) continue;
                try
                {
                    var record = JsonSerializer.Deserialize<HouseTappingRecord>(tapping.TappingData!,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (record != null && record.InitialReading >= 0)
                        initialByAccount[tapping.AccountId] = record.InitialReading;
                }
                catch (JsonException) { }
            }

            foreach (var account in accountList)
                account.InitialReading = initialByAccount.GetValueOrDefault(account.Id, 0);
        }

        #endregion
    }
}
