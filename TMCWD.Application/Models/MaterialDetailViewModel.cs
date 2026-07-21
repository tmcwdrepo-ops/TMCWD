using TMCWD.Model.CustomerSupport;
using TMCWD.Model.Engineering;

namespace TMCWD.Application.Models
{
    public class MaterialDetailViewModel
    {
        public MaterialDetailViewModel() { }

        public Material Material { get; set; } = new Material();

        public Inventory InventoryItem { get; set; } = new Inventory();

        public bool IsNegativeStock => this.Material.RequestedQuantity > this.InventoryItem.Quantity;

    }
}
