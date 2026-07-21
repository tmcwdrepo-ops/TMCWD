using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class InventoryService : IInventoryService
    {

        private readonly UserDbContext _dbContext;

        public InventoryService(UserDbContext context)
        {
            _dbContext = context;
        }

        public async Task<Inventory?> Get(int id)
        {
            var inventory = await _dbContext.Inventories.Where(x => x.Id == id).FirstOrDefaultAsync();
            return inventory;
        }

        public async Task<IEnumerable<Inventory>?> GetAll()
        {
            var inventory = _dbContext.Inventories;
            return await inventory.ToListAsync();
        }

        public async Task<IEnumerable<Inventory>?> GetByName(string name)
        {
            var inventory = _dbContext.Inventories.Where(x => x.Name.ToLower() == name.ToLower());
            return await inventory.ToListAsync();
        }

        public async Task<Inventory?> SaveUpdate(int userId, Inventory inventory)
        {
            inventory.DateUpdated = DateTime.Now;
            if (inventory.Id > 0)
            {
                inventory.UpdatedBy = userId;
                _dbContext.Inventories.Update(inventory);
            }
            else
            {
                inventory.CreatedBy = userId;
                inventory.DateCreated = DateTime.Now;
                _dbContext.Inventories.Add(inventory);
            }

            await _dbContext.SaveChangesAsync();
            return inventory;
        }

    }
}
