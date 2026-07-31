using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IAccountService
    {

        Task<Account?> Get(int id);
        Task<Account?> GetByAccountNumber(string accountNumber);
        Task<IEnumerable<Account>> GetByCustomerId(int customerId);
        Task<Account?> GetByMeterNumber(string meterNumber);
        Task<IEnumerable<Account>> GetByZoneAndBook(int zone, int book);
        Task<Account> SaveUpdate(int userId, Account account);
        Task<IEnumerable<Account>> GetAccounts();
        Task<IEnumerable<Account>> GetByZoneBookId(int zoneBookId);
        Task<IEnumerable<Account>> GetByZoneBookAndSequence(int zone, int book, int from, int to);

    }
}
