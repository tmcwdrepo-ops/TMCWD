using System;
using System.Net.Http.Json;
using TMCWD.Model.Billing;
using TMCWD.Services;

namespace TMCWD.Billing
{
    public class OtherChargeTransaction
    {

        #region fields

        private readonly WebService _service;

        #endregion

        #region constructors

        public OtherChargeTransaction(WebService service)
        {
            _service = service;
        }

        #endregion

        #region private

        private OtherCharge ConvertJsonToOtherCharge(string json)
        {
            var serializerOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            return System.Text.Json.JsonSerializer.Deserialize<OtherCharge>(json, serializerOptions) ?? new OtherCharge();
        }

        private List<OtherCharge> ConvertJsonToOtherCharges(string json)
        {
            var serializerOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            return System.Text.Json.JsonSerializer.Deserialize<List<OtherCharge>>(json, serializerOptions) ?? new List<OtherCharge>();
        }

        #endregion

        #region methods

        public async Task<OtherCharge> Get(int id)
        {
            var response = await _service.Client.GetAsync($"api/OtherCharges/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToOtherCharge(data);
        }

        public async Task<List<OtherCharge>> GetAll()
        {
            var response = await _service.Client.GetAsync($"api/OtherCharges/GetAll");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return new List<OtherCharge>();
            return ConvertJsonToOtherCharges(data);
        }

        public async Task<List<OtherCharge>> GetByClassificationId(int classificationId)
        {
            var response = await _service.Client.GetAsync($"api/OtherCharges/GetByClassificationId/{classificationId}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return new List<OtherCharge>();
            return ConvertJsonToOtherCharges(data);
        }

        public async Task<OtherCharge> SaveUpdate(int userId, OtherCharge otherCharge)
        {
            var content = JsonContent.Create(otherCharge);
            var response = await _service.Client.PostAsync($"api/OtherCharges/SaveUpdate/{userId}", content);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToOtherCharge(data);
        }

        #endregion

    }
}
