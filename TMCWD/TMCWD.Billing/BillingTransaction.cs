using System.Net.Http.Json;
using System.Text.Json;
using TMCWD.Services;
using TMCWD.Model.Billing.Interfaces;
using BillingModel = TMCWD.Model.Billing.Billing;

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
            return JsonSerializer.Deserialize<BillingModel>(json, serializerOptions);
        }

        public List<BillingBase> ConverJsonToBillings(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            var billings = JsonSerializer.Deserialize<List<BillingModel>>(json, serializerOptions) ?? new();
            return billings.Cast<BillingBase>().ToList();
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
            if (!response.IsSuccessStatusCode) return null;
            return ConverJsonToBillings(data);
        }

        public async Task<BillingBase> GetByReference(string reference)
        {
            var response = await _service.Client.GetAsync($"api/Billing/GetByRefence/{reference}");
            var data = await response.Content.ReadAsStringAsync();
            return ConvertJsonToBilling(data);
        }

        public async Task<List<BillingBase>> GetByReadingId(int readingId)
        {
            if (readingId == 0) throw new Exception("Reading id is not provided");

            var response = await _service.Client.GetAsync($"api/Billing/GetByReadingId/{readingId}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConverJsonToBillings(data);
        }

        public async Task<List<BillingBase>> GetByReadings(List<int> readingIds)
        {
            var queryParam = string.Join("&", readingIds.Select(x => $"ids={x}"));
            var response = await _service.Client.GetAsync($"api/Billing/GetByReadings?{queryParam}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConverJsonToBillings(data);
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
