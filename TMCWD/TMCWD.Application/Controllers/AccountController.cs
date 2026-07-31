using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;
using TMCWD.Application.Models;
using TMCWD.Billing;
using TMCWD.CustomerSupport;
using TMCWD.Model.Administrator;
using TMCWD.Model.Billing;
using TMCWD.Model.CustomerSupport;
using TMCWD.Services;

namespace TMCWD.Application.Controllers
{
    public class AccountController : Controller
    {

        private readonly AuthenticatedUserService _authenticatedUserService;
        private readonly CustomerTransaction _customerTransaction;
        private readonly AccountTransaction _accountTransaction;
        private readonly BillingTransaction _billingTransaction;
        private readonly ReadingSheetTransaction _readingSheetTransaction;
        private readonly ReadingTransaction _readingTransaction;

        public AccountController(AuthenticatedUserService authenticatedUserService, 
            CustomerTransaction customerTransaction, 
            AccountTransaction accountTransaction,
            BillingTransaction billingTransaction,
            ReadingSheetTransaction readingSheetTransaction,
            ReadingTransaction readingTransaction)
        {
            _authenticatedUserService = authenticatedUserService;
            _customerTransaction = customerTransaction;
            _accountTransaction = accountTransaction;
            _billingTransaction = billingTransaction;
            _readingSheetTransaction = readingSheetTransaction;
            _readingTransaction = readingTransaction;
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

        [HttpGet]
        public async Task<IActionResult> GetByZoneBookAndSequence(DateTime billingDate, int zone, int book, int seqFrom, int seqTo, int assignedTo)
        {
            var accounts = await _accountTransaction.GetByZoneBookAndSequence(zone, book, seqFrom, seqTo);

            if (accounts == null) return NotFound();

            var customerIds = accounts.Select(x => x.CustomerId).ToList();
            var accountIds = accounts.Select(x => x.Id).ToList();

            Task<List<Customer>> getCustomersTask = _customerTransaction.GetCustomersFromIds(customerIds);
            Task<ReadingSheet> getReadingSheets = _readingSheetTransaction.GetByBillingDateAndAssignedTo(zone, book, billingDate, assignedTo);

            await Task.WhenAll(getCustomersTask, getReadingSheets);

            var customers = getCustomersTask.Result;
            var readingSheet = (getReadingSheets.Result) ?? new ReadingSheet();

            var readings = await _readingTransaction.GetByReadingSheetId(readingSheet.Id) ?? new List<Reading>();

            var returnValue = from accts in accounts
                              join custs in customers on accts.CustomerId equals custs.Id
                              join rdngs in readings on accts.Id equals rdngs.AccountId into acctRdngs
                              from rdngs in acctRdngs.DefaultIfEmpty()
                              select new
                              {
                                  CustomerId = custs.Id,
                                  AccountId = accts.Id,
                                  AcctNo = accts.AccountNumber,
                                  Name = $"{custs.Lastname}, {custs.Firstname} {custs.Middlename}",
                                  Address = $"{accts.FullAddress}",
                                  Barangay = accts.Barangay,
                                  type = accts.Classification.ToString(),
                                  Zone = zone,
                                  Book = book,
                                  Billed = rdngs?.Status == ReadingStatus.Completed ? true : false,
                                  Sequence = accts.Sequence
                              };

            if (returnValue == null) return NotFound();
            return Ok(returnValue);
        }

    }

}
