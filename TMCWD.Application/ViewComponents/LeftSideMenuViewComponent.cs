using Microsoft.AspNetCore.Mvc;
using TMCWD.Services;

namespace TMCWD.Application.ViewComponents
{
    public class LeftSideMenuViewComponent : ViewComponent
    {

        private readonly AuthenticatedUserService _service;

        public LeftSideMenuViewComponent(AuthenticatedUserService service)
        {
            _service = service;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            return View("Default", _service.User.Role);
        }

    }
}
