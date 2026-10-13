using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Text;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;
using TMCWD.Utility.Generic;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : Controller
    {

        private readonly IAccountService _accountService;
        public AccountController(IAccountService service)
        {
            _accountService = service;
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetAll")]
        public async Task<ActionResult> GetAll()
        {
            var accounts = await _accountService.GetAccounts();
            if (accounts == null || !accounts.Any()) return NotFound();
            return Ok(accounts);
        }

        [HttpPost("SaveUpdate/{userId}")]
        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "CustomerService")]
        public async Task<ActionResult> SaveUpdate(int userId, [FromBody] TMCWD.Model.CustomerSupport.AccountWriteRequest request)
        {
            //StringBuilder sb = new();

            //if (String.IsNullOrEmpty(account.FullAddress.Trim())) sb.AppendLine("Address is required to create an account.");
            //if (String.IsNullOrEmpty(account.MeterNumber.Trim())) sb.AppendLine("Meter number is required to create an account.");

            if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out userId) || userId <= 0)
                return Unauthorized();
            Account updatedAccount;
            try { updatedAccount = await _accountService.SaveUpdate(userId, new Account {
                Id = request.Id, CustomerId = request.CustomerId, MeterNumber = request.MeterNumber,
                MeterSize = request.MeterSize, Classification = request.Classification,
                UnitNumber = request.UnitNumber, Building = request.Building, HouseNumber = request.HouseNumber,
                Street = request.Street, Barangay = request.Barangay, IsCurrentAddress = request.IsCurrentAddress }); }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
            if (updatedAccount == null || updatedAccount.Id <= 0) return BadRequest("Account was not created due to some issue(s).");

            return Ok(updatedAccount);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("Get/{id}")]
        public async Task<ActionResult> Get(int id)
        {
            var account = await _accountService.Get(id);

            if (account == null) return NotFound($"Account with id {id} was not found.");

            return Ok(account);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetByAccountNumber/{accountNumber}")]
        public async Task<ActionResult> GetByAccountNumber(string accountNumber)
        {
            var account = await _accountService.GetByAccountNumber(accountNumber);

            if (account == null) return NotFound($"Account with account number {accountNumber} was not found.");

            return Ok(account);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetByCustomerId/{customerId}")]
        public async Task<ActionResult> GetByCustomerId(int customerId)
        {
            var accounts = await _accountService.GetByCustomerId(customerId);

            if (accounts == null || !accounts.Any()) return NotFound($"Account(s) with customer id {customerId} was not found.");

            return Ok(accounts);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetByMeterNumber/{meterNumber}")]
        public async Task<ActionResult> GetByMeterNumber(string meterNumber)
        {
            var account = await _accountService.GetByMeterNumber(meterNumber);

            if (account == null) return NotFound($"Account with meter number {meterNumber} was not found.");

            return Ok(account);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetByZoneAndBook/{zone}/{book}")]
        public async Task<ActionResult> GetByZoneAndBook(int zone, int book)
        {
            var accounts = await _accountService.GetByZoneAndBook(zone, book);
            if (accounts == null || !accounts.Any()) return NotFound($"No accounts were found.");
            return Ok(accounts);

        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("GetByZoneBookAndSequence/{zone}/{book}/{seqFrom}/{seqTo}")]
        public async Task<IActionResult> GetByZoneBookAndSequence(int zone, int book, int seqFrom, int seqTo)
        {
            var accounts = await _accountService.GetByZoneBookAndSequence(zone, book, seqFrom, seqTo);
            if (accounts == null || !accounts.Any()) return NotFound();
            return Ok(accounts);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "BillingOperations")]
        [HttpGet("Search")]
        public async Task<IActionResult> Search([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 1)
                return Ok(new List<Account>());

            var accounts = await _accountService.Search(q);
            return Ok(accounts);
        }
    }

}
