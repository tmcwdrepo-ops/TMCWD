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

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<ActionResult> SaveUpdate(int userId, [FromBody] Account account)
        {
            //StringBuilder sb = new();

            //if (String.IsNullOrEmpty(account.FullAddress.Trim())) sb.AppendLine("Address is required to create an account.");
            //if (String.IsNullOrEmpty(account.MeterNumber.Trim())) sb.AppendLine("Meter number is required to create an account.");

            var updatedAccount = await _accountService.SaveUpdate(userId, account);
            if (updatedAccount == null || updatedAccount.Id <= 0) return BadRequest("Account was not created due to some issue(s).");

            return Ok(updatedAccount);
        }

        [HttpGet("Get/{id}")]
        public async Task<ActionResult> Get(int id)
        {
            var account = await _accountService.Get(id);

            if (account == null) return NotFound($"Account with id {id} was not found.");

            return Ok(account);
        }

        [HttpGet("GetByAccountNumber/{accountNumber}")]
        public async Task<ActionResult> GetByAccountNumber(string accountNumber)
        {
            var account = await _accountService.GetByAccountNumber(accountNumber);

            if (account == null) return NotFound($"Account with account number {accountNumber} was not found.");

            return Ok(account);
        }

        [HttpGet("GetByCustomerId/{customerId}")]
        public async Task<ActionResult> GetByCustomerId(int customerId)
        {
            var accounts = await _accountService.GetByCustomerId(customerId);

            if (accounts == null || !accounts.Any()) return NotFound($"Account(s) with customer id {customerId} was not found.");

            return Ok(accounts);
        }

        [HttpGet("GetByMeterNumber/{meterNumber}")]
        public async Task<ActionResult> GetByMeterNumber(string meterNumber)
        {
            var account = await _accountService.GetByMeterNumber(meterNumber);

            if (account == null) return NotFound($"Account with meter number {meterNumber} was not found.");

            return Ok(account);
        }

        [HttpGet("GetByZoneAndBook/{zone}/{book}")]
        public async Task<ActionResult> GetByZoneAndBook(int zone, int book)
        {
            var accounts = await _accountService.GetByZoneAndBook(zone, book);
            if (accounts == null || !accounts.Any()) return NotFound($"No accounts were found.");
            return Ok(accounts);

    }
}
}
