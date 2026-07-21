using Microsoft.AspNetCore.Mvc;
using TMCWD.Administration;

namespace TMCWD.Application.ViewComponents
{
    public class UserNameViewComponent : ViewComponent
    {

        private readonly UserTransaction _userTransaction;

        public UserNameViewComponent(UserTransaction userTransaction)
        {
            _userTransaction = userTransaction;
        }

        public async Task<IViewComponentResult> InvokeAsync(int userId)
        {
            var user = await _userTransaction.Get(userId);
            return View("Default", $"{user.Name}");
        }

    }
}
