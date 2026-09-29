using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IDebitCreditMemoService
    {
        Task<DebitCreditMemo?> Get(long id);
        Task<List<DebitCreditMemo>> GetAll();
        Task<DebitCreditMemo> Save(int userId, DebitCreditMemo memo);
    }
}
