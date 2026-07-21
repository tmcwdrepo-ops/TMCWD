using TMCWD.Model.Administrator;
using TMCWD.Model.Engineering;
namespace TMCWD.Application.Models
{
    public class InventoryViewModel
    {

        public User CurrentUser { get; set; } = new User();
        public Inventory AddEditInventory { get; set; } = new Inventory();

        public List<Inventory> Inventory { get; set; } = new List<Inventory>();

        public string SearchString { get; set; } = string.Empty;

    }
}
