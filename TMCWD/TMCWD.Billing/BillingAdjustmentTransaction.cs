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

        /// <summary>
        /// Returns the full audit history for a billing reference, newest first.
        /// Returns an empty list on any failure rather than null so callers can
        /// always iterate the result safely.
        /// </summary>
        public async Task<List<BillingAdjustmentHistoryDto>> GetHistory(string billingReferenceId)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var response = await _service.Client.GetAsync(
                $"api/BillingAdjustment/History/{Uri.EscapeDataString(billingReferenceId)}");
            if (!response.IsSuccessStatusCode) return new List<BillingAdjustmentHistoryDto>();
            var data = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<BillingAdjustmentHistoryDto>>(data, options)
                ?? new List<BillingAdjustmentHistoryDto>();
        }

        public async Task<BillingAdjustment> SaveUpdate(int userId, BillingAdjustment adjustment, Guid operationId)
        {
            var content = JsonContent.Create(adjustment);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"api/BillingAdjustment/SaveUpdate/{userId}") { Content = content };
            request.Headers.Add("Idempotency-Key", operationId.ToString("D"));
            var response = await _service.Client.SendAsync(request);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToAdjustment(data);
        }

        public async Task<List<BillingAdjustment>?> SaveBatch(
            int userId,
            List<BillingAdjustment> adjustments, Guid operationId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"api/BillingAdjustment/SaveBatch/{userId}") {
                Content = JsonContent.Create(adjustments) };
            request.Headers.Add("Idempotency-Key", operationId.ToString("D"));
            using var response = await _service.Client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadAsStringAsync();
            return ConvertJsonToAdjustments(data);
        }
    }
}
