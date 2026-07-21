using Microsoft.AspNetCore.Mvc;
using TMCWD.Services;

namespace TMCWD.Application.ViewComponents
{
    public class LoggedInUserViewComponent : ViewComponent
    {

        private readonly AuthenticatedUserService _service;

        public LoggedInUserViewComponent(AuthenticatedUserService service)
        {
            _service = service;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            return View("Default", _service.User.Name);
        }

    }
}
