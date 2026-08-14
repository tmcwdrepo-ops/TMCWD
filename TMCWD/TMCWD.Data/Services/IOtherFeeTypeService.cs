using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IOtherFeeTypeService
    {
        Task<OtherFeeType> Get(int id);

        Task<List<OtherFeeType>> GetAll();

        Task<List<OtherFeeType>> GetByName(string name);

        Task<OtherFeeType> SaveUpdate(
            int userId,
            OtherFeeType otherFeeType);

        Task<bool> Delete(int id);
    }
}