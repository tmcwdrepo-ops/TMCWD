using Microsoft.AspNetCore.Mvc;
using TMCWD.CustomerSupport;
using TMCWD.Model.CustomerSupport;

namespace TMCWD.Application.ViewComponents
{
    public class AccountSelectionViewComponent : ViewComponent
    {

        private readonly AccountTransaction _transaction;
        public AccountSelectionViewComponent(AccountTransaction transaction)
        {
            _transaction = transaction;
        }

        public async Task<IViewComponentResult> InvokeAsync(int customerId)
        {
            if (customerId <= 0) return View("Default", new List<Account>());
            var accounts = await _transaction.GetByCustomerId(customerId);
            return View("Default", accounts);
        }

    }
}
