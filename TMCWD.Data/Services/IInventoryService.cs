using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public interface IInventoryService
    {
        Task<Inventory?> SaveUpdate(int userId, Inventory inventory);
        Task<Inventory?> Get(int id);
        Task<IEnumerable<Inventory>?> GetAll();
        Task<IEnumerable<Inventory>?> GetByName(string name);
    }
}
