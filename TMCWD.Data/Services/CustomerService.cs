using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services
{
    public class CustomerService : ICustomerService
    {

        private readonly UserDbContext _dbContext;

        public CustomerService(UserDbContext context)
        {
            _dbContext = context;
        }

        public async Task<Customer?> Get(int id)
        {
            var customer = await _dbContext.Customers.Where(x => x.Id == id).FirstOrDefaultAsync();
            return customer;
        }

        public async Task<IEnumerable<Customer>?> GetByName(string firstname, string lastname)
        {
            var customers = _dbContext.Customers.Where(x => x.Firstname.ToLower() == firstname.ToLower() || x.Lastname.ToLower() == lastname.ToLower()).AsQueryable();
            return await customers.ToListAsync();
        }

        public async Task<IEnumerable<Customer>?> GetCustomers()
        {
            var customers = _dbContext.Customers;
            return await customers.ToListAsync();
        }

        public async Task<Customer> SaveUpdate(int userId, Customer customer)
        {
            if (customer.Id > 0)
            {
                customer.Updatedby = userId;
                customer.DateUpdated = DateTime.Now;
                _dbContext.Customers.Update(customer);
            }
            else
            {
                customer.CreatedBy = userId;
                customer.DateCreated = DateTime.Now;
                _dbContext.Customers.Add(customer);
            }

            await _dbContext.SaveChangesAsync();
            return customer;
        }

        public async Task<IEnumerable<Customer>?> Search(string searchString)
        {
            var customers = _dbContext.Customers.Where(x => x.Firstname.ToLower().Contains(searchString.ToLower()) || x.Lastname.ToLower().Contains(searchString.ToLower()) || x.Email.ToLower().Contains(searchString.ToLower()) || (x.Firstname.ToLower() + " " + x.Lastname.ToLower()).Contains(searchString.ToLower()) || x.Email.Contains(searchString)).AsQueryable();
            return await customers.ToListAsync();
        }

    }
}
