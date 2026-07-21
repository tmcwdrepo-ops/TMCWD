using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;
using TMCWD.Application.Models;
using TMCWD.CustomerSupport;
using TMCWD.Model.Administrator;
using TMCWD.Model.CustomerSupport;
using TMCWD.Services;

namespace TMCWD.Application.Controllers
{
    public class AccountController : Controller
    {

        private readonly AuthenticatedUserService _authenticatedUserService;
        private readonly CustomerTransaction _customerTransaction;
        private readonly AccountTransaction _accountTransaction;

        public AccountController(AuthenticatedUserService authenticatedUserService, CustomerTransaction customerTransaction, AccountTransaction accountTransaction)
        {
            _authenticatedUserService = authenticatedUserService;
            _customerTransaction = customerTransaction;
            _accountTransaction = accountTransaction;
        }

        public async Task<IActionResult> Index(int customerId, int accountId = 0)
        {

            User currentUser = _authenticatedUserService.User;

            AccountViewModel model = new();
            model.Customer = await _customerTransaction.Get(customerId);
            model.PagedAccountList = await _accountTransaction.GetByCustomerId(customerId);
            model.CurrentUser = currentUser;

            if(accountId > 0)
            {
                model.AddEditAccount = await _accountTransaction.Get(accountId);
            }
            else model.AddEditAccount.CustomerId = customerId;

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> SaveAccount(AccountViewModel model)
        {
            User currentUser = _authenticatedUserService.User;

            await _accountTransaction.SaveUpdate(currentUser.Id, model.AddEditAccount);
            return RedirectToAction("Index", "Account", new { customerId = model.AddEditAccount.CustomerId });
        }

        public async Task<IActionResult> DeactivateAccount(int accountId)
        {
            Account acct = new();
            acct =  await _accountTransaction.Get(accountId);
            if(acct != null)
            {
                acct.Status = AccountStatus.Closed;
                await _accountTransaction.SaveUpdate(_authenticatedUserService.User.Id, acct);
                RedirectToAction("Index", "Account", new { customerId = acct.CustomerId });
            }
            return RedirectToAction("Index", "Account");
        }

        [HttpPost]
        public async Task<IActionResult> AddAccount([FromBody] object content)
        {
            if(content != null)
            {
                var stringJson = JsonSerializer.Serialize(content);
                using var doc = JsonDocument.Parse(stringJson);
                if (doc == null) return NoContent();
                JsonElement root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Null) return NoContent();

                var customerIdProperty = root.GetProperty("customerId");
                var accountAddressProperty = root.GetProperty("accountAddress");

                if (customerIdProperty.ValueKind == JsonValueKind.Null || accountAddressProperty.ValueKind == JsonValueKind.Null) return NoContent();
                int.TryParse(customerIdProperty.GetString(), out int custId);
                int customerId = custId;
                string? address = accountAddressProperty.GetString();
                if (customerId > 0 && !String.IsNullOrEmpty(address))
                {
                    Account account = new()
                    {
                        AccountNumber = "TMCWD-" + DateTime.Now.Ticks.ToString(),
                        CreatedBy = _authenticatedUserService.User.Id,
                        CustomerId = customerId,
                        DateCreated = DateTime.Now,
                        Status = AccountStatus.Pending,
                        DateUpdated = DateTime.Now,
                        IsCurrentAddress = false,
                        MeterNumber = string.Empty,
                        UpdatedBy = _authenticatedUserService.User.Id,
                    };
                    var savedAccount = await _accountTransaction.SaveUpdate(_authenticatedUserService.User.Id, account);
                    return Ok(savedAccount);
                }
                else return NoContent();
            }

            return NoContent();
        }

        [HttpPost]
        public async Task<IActionResult> SaveUpdateAccount([FromBody] Account account)
        {
            var updatedAccount = await _accountTransaction.SaveUpdate(_authenticatedUserService.User.Id, account);
            if (updatedAccount == null) return BadRequest();
            return Ok(updatedAccount);
        }

    }

}
