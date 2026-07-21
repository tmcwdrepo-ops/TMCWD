using Microsoft.AspNetCore.Mvc;
using TMCWD.CustomerSupport;

namespace TMCWD.Application.ViewComponents
{
    public class AccountDropdownViewComponent : ViewComponent
    {

        private readonly AccountTransaction _accountTransaction;

        public AccountDropdownViewComponent(AccountTransaction accountTransaction)
        {
            _accountTransaction = accountTransaction;
        }

        public async Task<IViewComponentResult> InvokeAsync(int customerId)
        {
            var accounts = await _accountTransaction.GetByCustomerId(customerId);
            return View("Default", accounts);
        }

    }
}
