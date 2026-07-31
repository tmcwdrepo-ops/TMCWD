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
    public class EWalletLogTransaction
    {

        #region fields

        private readonly WebService _service;

        #endregion

        #region constructors

        public EWalletLogTransaction(WebService service)
        {
            _service = service;
        }

        #endregion

        #region methods

        public EWalletBase ConvertJsonToEWalletTransaction(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<EWalletBase>(json, serializerOptions);
        }

        public List<EWalletBase> ConvertJsonToEWalletTransactions(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<EWalletBase>>(json, serializerOptions) ?? new();
        }

        public async Task<EWalletBase> Get(int id)
        {
            var response = await _service.Client.GetAsync($"api/EWalletTransaction/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToEWalletTransaction(data);
        }

        public async Task<List<EWalletBase>> GetByPaymentReference(string reference)
        {
            var response = await _service.Client.GetAsync($"api/EWalletTransaction/GetByPaymentReference/{reference}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToEWalletTransactions(data);
        }

        public async Task<List<EWalletBase>> GetByProcessedBy(int processdBy)
        {
            var response = await _service.Client.GetAsync($"api/EWalletTransaction/GetByProcessedBy/{processdBy}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToEWalletTransactions(data);
        }

        public async Task<EWalletBase> SaveUpdate(int userId, EWalletBase transaction)
        {
            var content = JsonContent.Create(transaction);
            var response = await _service.Client.PostAsync($"api/EWalletTransaction/SaveUpdate/{userId}", content);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToEWalletTransaction(data);
        }

        #endregion

    }
}
