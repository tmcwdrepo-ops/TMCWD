using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TMCWD.Model.Billing;
using TMCWD.Services;

namespace TMCWD.Billing
{
    public class PaymentCheckTransaction
    {

        #region fields

        private readonly WebService _service;

        #endregion

        #region constructors

        public PaymentCheckTransaction(WebService service)
        {
            _service = service;
        }

        #endregion

        #region methods

        public PaymentCheck ConvertJsonToPaymentCheck(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<PaymentCheck>(json, serializerOptions) ?? new();
        }

        public List<PaymentCheck> ConvertJsonToPaymentChecks(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<PaymentCheck>>(json, serializerOptions) ?? new();
        }

        public async Task<PaymentCheck> Get(int id)
        {
            var response = await _service.Client.GetAsync($"api/PaymentCheck/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToPaymentCheck(data);
        }

        public async Task<List<PaymentCheck>> GetAll()
        {
            var response = await _service.Client.GetAsync("api/PaymentCheck/GetAll");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToPaymentChecks(data);
        }

        public async Task<List<PaymentCheck>> GetByReference(string referenceId)
        {
            var response = await _service.Client.GetAsync($"api/PaymentCheck/GetByReference/{referenceId}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToPaymentChecks(data);
        }

        public async Task<PaymentCheck> SaveUpdate(int userId, PaymentCheck paymentCheck)
        {
            var content = JsonContent.Create(paymentCheck);
            var response = await _service.Client.PostAsync($"api/PaymentCheck/SaveUpdate/{userId}", content);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToPaymentCheck(data);
        }
            
        #endregion

    }
}
