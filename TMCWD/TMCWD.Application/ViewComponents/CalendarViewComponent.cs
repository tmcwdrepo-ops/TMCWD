using Microsoft.AspNetCore.Mvc;

namespace TMCWD.Application.ViewComponents
{
    public class CalendarViewComponent : ViewComponent
    {
        public async Task<IViewComponentResult> InvokeAsync(string id)
        {
            return View("Default", id);
        }
    }
}
