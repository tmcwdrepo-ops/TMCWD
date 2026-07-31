using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using TMCWD.Services;

namespace TMCWD.Billing
{
    public class EndpointTransaction
    {

        #region fields

        private readonly WebService _service;

        #endregion

        #region constructors

        public EndpointTransaction(WebService service)
        {
            _service = service;
        }

        #endregion

        #region methods

        public TMCWD.Model.Billing.Endpoint ConvertJsonToEndpoint(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<TMCWD.Model.Billing.Endpoint>(json, serializerOptions) ?? new();
        }

        public List<TMCWD.Model.Billing.Endpoint> ConvertJsonToEndpoints(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<TMCWD.Model.Billing.Endpoint>>(json, serializerOptions) ?? new();
        }

        public async Task<TMCWD.Model.Billing.Endpoint> Get(int id)
        {
            var response = await _service.Client.GetAsync($"api/Endpoint/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToEndpoint(data);
        }

        public async Task<List<TMCWD.Model.Billing.Endpoint>> GetByType(GatewayType type)
        {
            var response = await _service.Client.GetAsync($"api/Endpoint/GetByType/{(int)type}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToEndpoints(data);
        }

        public async Task<TMCWD.Model.Billing.Endpoint> GetByTypeAndName(GatewayType type, string name)
        {
            var response = await _service.Client.GetAsync($"api/Endpoint/GetByTypeAndName/{(int)type}/{name}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToEndpoint(data);
        }

        #endregion

    }
}
