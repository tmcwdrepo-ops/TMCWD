using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IMaterialService
    {

        Task<Material?> Get(int id);
        Task<List<Material>> GetAll();
        Task<List<Material>> GetByJobOrderId(int jobOrderId);
        Task<Material> SaveUpdate(int userId, int requestId, Material material);
        Task<Material> UpdateQuantityOrNewUnitCost(int userId, int requestId, Material updateMaterial);

        Task<bool> Delete(Material material);

    }
}
