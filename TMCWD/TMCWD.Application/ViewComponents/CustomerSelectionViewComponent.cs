using Microsoft.AspNetCore.Mvc;
using TMCWD.Application.Models;
using TMCWD.CustomerSupport;

namespace TMCWD.Application.ViewComponents
{
    public class CustomerSelectionViewComponent : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync()
        {
            return View("Default");
        }
    }
}
