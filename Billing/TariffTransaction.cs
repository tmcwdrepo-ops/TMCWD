using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using TMCWD.Model.Billing;
using TMCWD.Services;

namespace TMCWD.Billing
{
    public class TariffTransaction
    {
        #region fields

        private readonly WebService _service;

        #endregion

        #region constructors

        public TariffTransaction(WebService service)
        {
            _service = service;
        }

        #endregion

        #region methods

        public Tariff ConvertJsonToTariff(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true  };
            return JsonSerializer.Deserialize<Tariff>(json, serializerOptions) ?? new();
        }

        public List<Tariff> ConvertJsonToTariffs(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<Tariff>>(json, serializerOptions) ?? new();
        }

        public async Task<Tariff> Get(int id)
        {
            var response = await _service.Client.GetAsync($"api/Tariff/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToTariff(data);
        }

        public async Task<List<Tariff>> GetAll()
        {
            var response = await _service.Client.GetAsync("api/Tariff/GetAll");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToTariffs(data);
        }

        public async Task<List<Tariff>> GetByClassification(AccountClassification classification)
        {
            var response = await _service.Client.GetAsync($"api/Tariff/GetByClassification/{(int)classification}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToTariffs(data);
        }

        #endregion

    }
}
