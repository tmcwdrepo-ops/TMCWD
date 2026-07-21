using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using TMCWD.Model.Billing;
using TMCWD.Services;

namespace TMCWD.Billing
{
    public class ChargeTypeTransaction
    {

        #region fields

        private readonly WebService _webService;

        #endregion

        #region constructors

        public ChargeTypeTransaction(WebService webService)
        {
            _webService = webService;
        }

        #endregion

        #region private

        public ChargeType ConvertJsonToChargeType(string jsonString)
        {
            var serializerOptions = new System.Text.Json.JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return System.Text.Json.JsonSerializer.Deserialize<ChargeType>(jsonString, serializerOptions) ?? new ChargeType();
        }

        public List<ChargeType> ConvertJsonToChargeTypes(string jsonString)
        {
            var serializerOptions = new System.Text.Json.JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return System.Text.Json.JsonSerializer.Deserialize<List<ChargeType>>(jsonString, serializerOptions) ?? new List<ChargeType>();
        }

        #endregion

        #region methods

        public async Task<ChargeType> Get(int id)
        {
            var response = await _webService.Client.GetAsync($"api/ChargeType/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToChargeType(data);
        }

        public async Task<List<ChargeType>> GetAll()
        {
            var response = await _webService.Client.GetAsync($"api/ChargeType/GetAll");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return new List<ChargeType>();
            return ConvertJsonToChargeTypes(data);
        }

        public async Task<List<ChargeType>> GetByClassificationId(int classificationId)
        {
            var response = await _webService.Client.GetAsync($"api/ChargeType/GetByClassificationId/{classificationId}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return new List<ChargeType>();
            return ConvertJsonToChargeTypes(data);
        }

        public async Task<ChargeType> SaveUpdate(int chargeTypeId, ChargeType chargeType)
        {
            var content = System.Net.Http.Json.JsonContent.Create(chargeType);
            var response = await _webService.Client.PostAsync($"api/ChargeType/SaveUpdate/{chargeTypeId}", content);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToChargeType(data);
        }

        #endregion

    }
}
