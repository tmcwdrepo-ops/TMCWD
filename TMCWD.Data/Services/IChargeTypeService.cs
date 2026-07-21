using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IChargeTypeService
    {

        public Task<ChargeType> Get(int id);

        public Task<List<ChargeType>> GetAll();

        public Task<List<ChargeType>> GetByClassificationId(int classificationId);

        public Task<ChargeType> SaveUpdate(int userId, ChargeType chargeType);

    }
}
