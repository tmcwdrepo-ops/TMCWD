using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;
using TMCWD.Utility.Generic;

namespace TMCWD.Data.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class CustomerController : Controller
    {

        private readonly ICustomerService _customerService;

        public CustomerController(ICustomerService service)
        {
            _customerService = service;
        }

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<ActionResult<Customer>> SaveUpdate(int userId, [FromBody] Customer customer)
        {
            var updatedCustomer = await _customerService.SaveUpdate(userId, customer);
            if (updatedCustomer == null || updatedCustomer?.Id <= 0) BadRequest("Customer was not saved due to some issue(s)");
            return Ok(updatedCustomer);
        }

        [HttpGet("Get/{id}")]
        public async Task<ActionResult<Customer>> Get(int id)
        {
            var customer = await _customerService.Get(id);
            if (customer == null) return NotFound($"Customer with id {id} was not found.");
            return Ok(customer);
        }

        [HttpGet("GetByName/{firstname}/{lastname}")]
        public async Task<ActionResult<IEnumerable<Customer>>> GetByName(string firstname, string lastname)
        {
            var customers = await _customerService.GetByName(firstname, lastname);
            if (customers == null || !customers.Any()) return NotFound($"Customer with name {firstname} {lastname} cannot be found.");
            return Ok(customers);

        }

        [HttpGet("GetCustomers")]
        public async Task<ActionResult<IEnumerable<Customer>>> GetCustomers()
        {
            var customers = await _customerService.GetCustomers();
            if (customers == null || !customers.Any()) return NotFound("No customers found.");

            return Ok(customers);
        }

        [HttpGet("Search/{searchString}")]
        public async Task<ActionResult> Search(string searchString)
        {
            var customers = await _customerService.Search(searchString);
            if (customers == null || !customers.Any()) return NotFound($"No customers found matching the search string: {searchString}");
            return Ok(customers);
        }

    }
}
