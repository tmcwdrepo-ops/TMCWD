using Microsoft.AspNetCore.Mvc;
using System.Reflection.Metadata.Ecma335;

namespace TMCWD.Application.ViewComponents
{
    public class AlertViewComponent : ViewComponent
    {

        public async Task<IViewComponentResult> InvokeAsync()
        {
            return View("Default");
        }

    }
}
