using System.Net.Http.Json;
using System.Text.Json;
using TMCWD.Services;
using TMCWD.Model.Billing;

namespace TMCWD.Billing
{
    public class BillingAdjustmentTransaction
    {
        private readonly WebService _service;

        public BillingAdjustmentTransaction(WebService service) { _service = service; }

        private BillingAdjustment ConvertJsonToAdjustment(string json)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<BillingAdjustment>(json, options) ?? new();
        }

        private List<BillingAdjustment> ConvertJsonToAdjustments(string json)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<BillingAdjustment>>(json, options) ?? new();
        }

        public async Task<BillingAdjustment> Get(int id)
        {
            var response = await _service.Client.GetAsync($"api/BillingAdjustment/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToAdjustment(data);
        }

        public async Task<List<BillingAdjustment>> GetAll()
        {
            var response = await _service.Client.GetAsync("api/BillingAdjustment/GetAll");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToAdjustments(data);
        }

        public async Task<List<BillingAdjustment>> GetByReference(string reference)
        {
            var response = await _service.Client.GetAsync($"api/BillingAdjustment/GetByReference/{reference}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToAdjustments(data);
        }

        public async Task<BillingAdjustment> SaveUpdate(int userId, BillingAdjustment adjustment)
        {
            var content = JsonContent.Create(adjustment);
            var response = await _service.Client.PostAsync($"api/BillingAdjustment/SaveUpdate/{userId}", content);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToAdjustment(data);
        }
    }
}