using Microsoft.AspNetCore.Mvc;
using TMCWD.Billing;
using TMCWD.Services;

namespace TMCWD.Application.Controllers
{
    public class BillingController : Controller
    {

        #region fields

        private readonly AuthenticatedUserService _user;
        private readonly BillingTransaction _billTransaction;

        #endregion

        #region constructors

        public BillingController(AuthenticatedUserService user, BillingTransaction billTransaction)
        {
            _user = user;
            _billTransaction = billTransaction;
        }

        #endregion

        #region methods

        public async Task<IActionResult> GetBillById(int id)
        {
            
            return Ok();
        }

        public async Task<IActionResult> GetBillByBillPeriod(DateTime billPeriod)
        {
            return Ok();
        }

        #endregion

    }
}
