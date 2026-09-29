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

        public async Task<List<PaymentHistoryRecord>> GetPaymentHistoryByBillingReference(string referenceId)
        {
            var response = await _service.Client.GetAsync($"api/Payment/GetByBillingReference/{referenceId}");
            if (!response.IsSuccessStatusCode) return new List<PaymentHistoryRecord>();

            var data = await response.Content.ReadAsStringAsync();
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<PaymentHistoryRecord>>(data, serializerOptions)
                   ?? new List<PaymentHistoryRecord>();
        }

        public async Task<List<PaymentBase>> GetByCashier(int userId)
        {
            var response = await _service.Client.GetAsync($"api/Payment/GetByCashier/{userId}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToPaymentBases(data);
        }

        public async Task<List<PaymentHistoryRecord>> GetPaymentHistoryByCashier(int userId)
        {
            var response = await _service.Client.GetAsync($"api/Payment/GetByCashier/{userId}");
            if (!response.IsSuccessStatusCode) return new List<PaymentHistoryRecord>();

            var data = await response.Content.ReadAsStringAsync();
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<PaymentHistoryRecord>>(data, serializerOptions)
                   ?? new List<PaymentHistoryRecord>();
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

        public async Task<List<InvoiceRecord>> SearchInvoices(string query)
        {
            var response = await _service.Client.GetAsync(
                $"api/Payment/SearchInvoices?q={Uri.EscapeDataString(query)}");
            if (!response.IsSuccessStatusCode) return new();
            return await response.Content.ReadFromJsonAsync<List<InvoiceRecord>>() ?? new();
        }

        public async Task<List<PaymentHistoryRecord>> GetByDateRange(
            DateTime from, DateTime to, int cashierId = 0)
        {
            var url = $"api/Payment/GetByDateRange" +
                      $"?from={Uri.EscapeDataString(from.ToString("yyyy-MM-dd"))}" +
                      $"&to={Uri.EscapeDataString(to.ToString("yyyy-MM-dd"))}" +
                      (cashierId > 0 ? $"&cashierId={cashierId}" : "");

            var response = await _service.Client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return new();

            var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return await response.Content.ReadFromJsonAsync<List<PaymentHistoryRecord>>(opts) ?? new();
        }

        public async Task<InvoiceRecord?> GetInvoice(string invoiceNumber)
        {
            var response = await _service.Client.GetAsync(
                $"api/Payment/Invoice/{Uri.EscapeDataString(invoiceNumber)}");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<InvoiceRecord>();
        }

        public async Task<(bool Success, string Message)> CancelInvoice(
            int userId,
            CancelInvoiceRequest request)
        {
            var response = await _service.Client.PostAsJsonAsync(
                $"api/Payment/CancelInvoice/{userId}", request);
            if (response.IsSuccessStatusCode) return (true, string.Empty);
            var data = await response.Content.ReadAsStringAsync();
            try
            {
                using var json = JsonDocument.Parse(data);
                if (json.RootElement.TryGetProperty("message", out var message))
                    return (false, message.GetString() ?? "Invoice could not be cancelled.");
            }
            catch (JsonException) { }
            return (false, "Invoice could not be cancelled.");
        }

        #endregion

    }
}
