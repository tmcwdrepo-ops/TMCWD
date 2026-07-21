using Microsoft.AspNetCore.Mvc;
using TMCWD.CustomerSupport;
using TMCWD.Model.CustomerSupport;
using TMCWD.Services;

namespace TMCWD.Application.Controllers
{
    public class CustomerController : Controller
    {

        private readonly AuthenticatedUserService _authenticatedUserService;
        private readonly CustomerTransaction _customerTransaction;

        public CustomerController(AuthenticatedUserService authenticatedUserService, CustomerTransaction customerTransaction)
        {
            _authenticatedUserService = authenticatedUserService;
            _customerTransaction = customerTransaction;
        }

        [HttpPost]
        public async Task<IActionResult> SaveUpdateCustomer(Customer customer)
        {
            var updateCustomer = await _customerTransaction.SaveUpdate(_authenticatedUserService.User.Id, customer);
            if (customer == null) return BadRequest();
            return Ok(updateCustomer);
        }
    }
}
