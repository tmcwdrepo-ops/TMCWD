using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface ITariffService
    {
        #region members

        public Task<Tariff> Get(int id);

        public Task<List<Tariff>> GetAll();

        public Task<List<Tariff>> GetByClassification(int classification);

        #endregion
    }
}
