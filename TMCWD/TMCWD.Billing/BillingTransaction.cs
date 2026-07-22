using System.Net.Http.Json;
using System.Text.Json;
using TMCWD.Services;
using TMCWD.Model.Billing.Interfaces;

namespace TMCWD.Billing
{
    public class BillingTransaction
    {

        #region fields

        private readonly WebService _service;

        #endregion

        #region constructors

        public BillingTransaction(WebService service) { _service = service; }

        #endregion

        #region methods

        public BillingBase ConvertJsonToBilling(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<BillingBase>(json, serializerOptions);
        }

        public List<BillingBase> ConverJsonToBillings(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };  
            return JsonSerializer.Deserialize<List<BillingBase>>(json, serializerOptions) ?? new();
        }

        public async Task<BillingBase> Get(int id)
        {
            var response = await _service.Client.GetAsync($"api/Billing/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToBilling(data);
        }

        public async Task<List<BillingBase>> GetAll()
        {
            var response = await _service.Client.GetAsync($"api/Billing/GetAll");
            var data = await response.Content.ReadAsStringAsync();
            return ConverJsonToBillings(data);
        }

        public async Task<BillingBase> GetByReference(string reference)
        {
            var response = await _service.Client.GetAsync($"api/Billing/GetByRefence/{reference}");
            var data = await response.Content.ReadAsStringAsync();
            return ConvertJsonToBilling(data);
        }

        public async Task<BillingBase> SaveUpdate(int userId, BillingBase billing)
        {
            var content = JsonContent.Create(billing);
            var response = await _service.Client.PostAsync($"api/Billing/SaveUpdate/{userId}", content);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToBilling(data);
        }

        #endregion

    }
}
