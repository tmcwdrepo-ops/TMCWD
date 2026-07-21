using TMCWD.Data.Entities;
namespace TMCWD.Data.Services
{
    public interface ICustomerService
    {
        Task<Customer> SaveUpdate(int userId, Customer customer);

        Task<Customer?> Get(int id);

        Task<IEnumerable<Customer>?> GetByName(string firstname, string lastname);

        Task<IEnumerable<Customer>?> GetCustomers();

        Task<IEnumerable<Customer>?> Search(string searchString);
    }
}
