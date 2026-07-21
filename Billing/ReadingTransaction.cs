using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TMCWD.Model.Billing;
using TMCWD.Model.CustomerSupport;
using TMCWD.Services;

namespace TMCWD.Billing
{
    public class ReadingTransaction
    {

        #region fields

        private readonly WebService _service;

        #endregion

        #region constructors

        public ReadingTransaction(WebService service)
        {
            _service = service;
        }

        #endregion

        #region methods

        public Reading ConvertJsonToReading(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<Reading>(json, serializerOptions) ?? new();
        }

        public List<Reading> ConvertJsonToReadings(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<Reading>>(json, serializerOptions) ?? new();
        }

        public async Task<Reading> Get(int id)
        {
            var response = await _service.Client.GetAsync($"api/Reading/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToReading(data);
        }

        public async Task<List<Reading>> GetByAccount(int accountId)
        {
            var response = await _service.Client.GetAsync($"api/Reading/GetByAccount/{accountId}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToReadings(data);
        }

        public async Task<List<Reading>> GetByAccountAndBillingPeriod(int accountId, DateTime billingPeriod)
        {
            var response = await _service.Client.GetAsync($"api/Reading/GetByAccountAndBillingPeriod/{accountId}/{billingPeriod}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToReadings(data);
        }

        public async Task<List<Reading>> GetByReader(int readerId)
        {
            var response = await _service.Client.GetAsync($"api/Reading/GetByAccountAndBillingPeriod/{readerId}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToReadings(data);
        }

        public async Task<List<Reading>> GetByZoneAndBook(int zone, int book)
        {
            var response = await _service.Client.GetAsync($"api/Reading/GetByZoneAndBook/{zone}/{book}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToReadings(data);
        }

        public async Task<Reading> GetByZoneAndBook(int userId, Reading reading)
        {
            var content = JsonContent.Create(reading);
            var response = await _service.Client.GetAsync($"api/Reading/GetByZoneAndBook/{userId}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToReading(data);
        }

        public async Task<Reading> GetAccountCurrentReading(int accountId)
        {
            var response = await _service.Client.GetAsync($"api/Reading/GetAccountCurrentReading/{accountId}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToReading(data);
        }

        public async Task<Reading> GetAccountPreviousReading(int accountId)
        {
            var response = await _service.Client.GetAsync($"api/Reading/GetAccountPreviousReading/{accountId}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToReading(data);
        }

        #endregion

    }
}
