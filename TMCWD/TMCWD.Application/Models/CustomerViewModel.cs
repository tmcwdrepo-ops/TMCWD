
using TMCWD.Model.Administrator;
using TMCWD.Model.CustomerSupport;
namespace TMCWD.Application.Models
{
    public class CustomerViewModel
    {

        public Customer AddEditCustomer { get; set; } = new Customer();

        public List<Customer> PagedCustomerList { get; set; } = new List<Customer>();

        public List<Account> CustomerAccounts { get; set; } = new List<Account>();

    }
}
