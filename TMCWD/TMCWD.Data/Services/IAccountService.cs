using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IAccountService
    {

        Task<Account?> Get(int id);
        Task<Account?> GetByAccountNumber(string accountNumber);
        Task<IEnumerable<Account>> GetByCustomerId(int customerId);
        Task<Account?> GetByMeterNumber(string meterNumber);
        Task<List<Account>> GetAccountsByZoneBookId(int zoneBookId);
        Task<List<Account>> GetByZoneAndBook(int zone, int book);
        Task<Account> SaveUpdate(int userId, Account account);
        Task<IEnumerable<Account>> GetAccounts();
    }
}
