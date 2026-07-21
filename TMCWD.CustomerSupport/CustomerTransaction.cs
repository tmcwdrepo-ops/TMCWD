using System.Net.Http.Json;
using TMCWD.Model.CustomerSupport;
using TMCWD.Model.Interfaces;
using System.Text.Json;
using TMCWD.Services;

namespace TMCWD.CustomerSupport
{
    public class CustomerTransaction : TransactionBase
    {

        #region fields

        private readonly WebService _webService;

        #endregion

        #region constructors
        public CustomerTransaction(WebService webService) { _webService = webService; }

        #endregion

        #region public methods

        public Customer ConvertJsonToCustomer(string json)
        {
            var serializeOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<Customer>(json, serializeOptions) ?? new Customer();
        }

        public List<Customer> ConvertJsonToCustomers(string json)
        {
            var serializeOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<Customer>>(json, serializeOptions) ?? new List<Customer>();
        }

        public async Task<Customer> SaveUpdate(int userId, Customer customer)
        {
            var content = JsonContent.Create(customer);

            var response = await _webService.Client.PostAsync($"api/Customer/SaveUpdate/{userId}", content);

            var data = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return null;

            return this.ConvertJsonToCustomer(data);
        }

        public async Task<Customer> Get(int id)
        {
            var response = await _webService.Client.GetAsync($"api/Customer/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if(!response.IsSuccessStatusCode) return null;
            return this.ConvertJsonToCustomer(data);
        }

        public async Task<List<Customer>> GetByName(string firstname, string lastname)
        {
            var response = await _webService.Client.GetAsync($"api/Customer/GetByName/{firstname}/{lastname}");
            var data = await response.Content.ReadAsStringAsync();
            if(!response.IsSuccessStatusCode) return null;
            return this.ConvertJsonToCustomers(data);
        }

        public async Task<List<Customer>> GetCustomers()
        {
            var response = await _webService.Client.GetAsync("api/Customer/GetCustomers");
            var data = await response.Content.ReadAsStringAsync();
            if(!response.IsSuccessStatusCode) return null;
            return this.ConvertJsonToCustomers(data);
        }

        public async Task<List<Customer>> Search(string searchString)
        {
            var response = await _webService.Client.GetAsync($"api/Customer/Search/{searchString}");
            var data = await response.Content.ReadAsStringAsync();
            if(!response.IsSuccessStatusCode) return null;
            return this.ConvertJsonToCustomers(data);
        }

        #endregion

    }
}
