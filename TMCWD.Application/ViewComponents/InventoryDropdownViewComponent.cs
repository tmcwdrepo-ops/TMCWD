using Microsoft.AspNetCore.Mvc;
using TMCWD.Model.Engineering;

namespace TMCWD.Application.ViewComponents
{
    public class InventoryDropdownViewComponent : ViewComponent
    {

        public InventoryDropdownViewComponent() { }

        public async Task<IViewComponentResult> InvokeAsync(string selectId, List<Inventory> inventoryItems)
        {
            return View("Default", new { Id = selectId, Items = inventoryItems });
        }

    }
}
