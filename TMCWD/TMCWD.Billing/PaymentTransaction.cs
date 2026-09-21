using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TMCWD.Model.Billing;
using TMCWD.Model.Billing.Interfaces;
using TMCWD.Services;

namespace TMCWD.Billing
{
    public class PaymentTransaction
    {

        #region fields

        private readonly WebService _service;

        #endregion

        #region constructors

        public PaymentTransaction(WebService service)
        {
            _service = service;
        }

        #endregion

        #region methods

        public PaymentBase ConvertJsonToPaymentBase(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<PaymentBase>(json, serializerOptions);
        }

        public List<PaymentBase> ConvertJsonToPaymentBases(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<PaymentBase>>(json, serializerOptions) ?? new();
        }

        public async Task<PaymentBase> Get(int id)
        {
            var response = await _service.Client.GetAsync($"api/Payment/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToPaymentBase(data);
        }

        public async Task<List<PaymentBase>> GetByBillingReference(string referenceId)
        {
            var response = await _service.Client.GetAsync($"api/Payment/GetByBillingReference/{referenceId}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToPaymentBases(data);
        }

        public async Task<List<PaymentBase>> GetByCashier(int userId)
        {
            var response = await _service.Client.GetAsync($"api/Payment/GetByCashier/{userId}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToPaymentBases(data);
        }

        public async Task<List<PaymentBase>> GetByMethod(PaymentMethod method)
        {
            var response = await _service.Client.GetAsync($"api/Payment/GetByMethod/{(int)method}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToPaymentBases(data);
        }

        public async Task<List<PaymentBase>> GetByMacAddresss(string macAddress)
        {
            var response = await _service.Client.GetAsync($"api/Payment/GetByMacAddress/{macAddress}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToPaymentBases(data);
        }

        public async Task<PaymentBase> GetByReference(string reference)
        {
            var response = await _service.Client.GetAsync($"api/Payment/GetByPaymentReference/{reference}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToPaymentBase(data);
        }

        public async Task<PaymentBase> SaveUpdate(int userId, PaymentBase payment)
        {
            var content = JsonContent.Create(payment);
            var response = await _service.Client.PostAsync($"api/Payment/SaveUpdate/{userId}", content);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToPaymentBase(data);
        }

        public async Task<(bool Success, string Message)> ProcessCollectionPayment(
            int userId,
            TMCWD.Model.Billing.Requests.CollectionPaymentRequest request)
        {
            var response = await _service.Client.PostAsJsonAsync(
                $"api/Payment/ProcessCollection/{userId}", request);

            if (response.IsSuccessStatusCode)
                return (true, string.Empty);

            var data = await response.Content.ReadAsStringAsync();
            try
            {
                using var json = JsonDocument.Parse(data);
                if (json.RootElement.TryGetProperty("message", out var message))
                    return (false, message.GetString() ?? "Payment could not be completed.");
                if (json.RootElement.TryGetProperty("detail", out var detail) &&
                    !string.IsNullOrWhiteSpace(detail.GetString()))
                    return (false, detail.GetString()!);
                if (json.RootElement.TryGetProperty("title", out var title) &&
                    !string.IsNullOrWhiteSpace(title.GetString()))
                    return (false, title.GetString()!);
            }
            catch (JsonException)
            {
                // Fall through to the user-safe response below.
            }

            return (false,
                $"Payment service returned {(int)response.StatusCode} ({response.ReasonPhrase}).");
        }

        #endregion

    }
}
