using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
using TMCWD.Utility.Generic;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RequestController : Controller
    {

        private readonly IRequestService _requestService;
        private readonly ICustomerService _customerService;
        private readonly IAccountService _accountService;

        public RequestController(IRequestService requestService, ICustomerService customerService, IAccountService accountService)
        {
            _requestService = requestService;
            _customerService = customerService;
            _accountService = accountService;
        }

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<ActionResult<Request>> SaveUpdate(int userId, [FromBody] Request request)
        {

            var insertedRequest = await _requestService.SaveUpdate(userId, request);

            if (insertedRequest == null || insertedRequest.Id <= 0) BadRequest("Request was not created");

            return Ok(insertedRequest);
        }

        [HttpGet("Get/{id}")]
        public async Task<ActionResult<Request>> Get(int id)
        {
            var data = await _requestService.Get(id);
            if (data == null) return NotFound($"Request with id {id} not found");
            return Ok(data);
        }

        [HttpGet("GetRequests")]
        public async Task<ActionResult<IEnumerable<Request>>> GetRequests()
        {
            var data = await _requestService.GetAll();
            if (data == null || !data.Any()) return NotFound("No request(s) found");
            return Ok(data);
        }

        [HttpGet("GetByUserId/{userId}")]
        public async Task<ActionResult<IEnumerable<Request>>> GetByUserId(int userId)
        {
            var data = await _requestService.GetByUserId(userId);
            if (data == null || !data.Any()) return NotFound($"No request(s) found for user with id {userId}");
            return Ok(data);
        }

        [HttpGet("GetByCustomerId/{customerId}")]
        public async Task<ActionResult<IEnumerable<Request>>> GetByCustomerId(int customerId)
        {
            var data = await _requestService.GetByCustomerId(customerId);
            if (data == null || !data.Any()) return NotFound($"No request(s) found for customer with id {customerId}");
            return Ok(data);
        }

        [HttpGet("SearchRequest/{searchString}")]
        public async Task<ActionResult<IEnumerable<Request>>> SearchRequest(string searchString)
        {

            var allRequests = await _requestService.GetAll();
            var customers = await _customerService.GetCustomers();
            var accounts = await _accountService.GetAccounts();

            if (allRequests == null || customers == null || accounts == null) return NotFound();

            var requests = from request in allRequests
                           join customer in customers on request.CustomerId equals customer.Id
                          join account in accounts on request.AccountId equals account.Id
                          where $"{customer.Firstname} {customer.Lastname}".Contains(searchString) || account.AccountNumber.Contains(searchString) || $"{customer.Lastname} {customer.Firstname}".Contains(searchString)
                          select request;
            if (requests == null || !requests.Any()) return NotFound($"Request with value {searchString} was not found.");

            return Ok(requests);
        }

    }
}
