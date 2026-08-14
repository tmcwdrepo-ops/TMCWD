using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface ITariffService
    {
        Task<Tariff> Get(int id);

        Task<List<Tariff>> GetAll();

        Task<List<Tariff>> GetByClassification(int classification);
    }
}