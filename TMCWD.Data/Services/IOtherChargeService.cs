using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IOtherChargeService
    {
        public Task<OtherCharge> Get(int id);

        public Task<List<OtherCharge>> GetAll();

        public Task<List<OtherCharge>> GetByReference(string referenceId);

        public Task<OtherCharge> SaveUpdate(int userId, OtherCharge otherCharge);

    }
}
