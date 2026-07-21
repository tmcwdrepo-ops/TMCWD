using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TMCWD.Model.Billing;
using TMCWD.Services;

namespace TMCWD.Billing
{
    public class PenaltyTransaction
    {

        #region fields
        private readonly WebService _service;
        #endregion

        #region constructors
        public PenaltyTransaction(WebService service)
        {
            _service = service;
        }
        #endregion

        #region private

        private Penalty ConvertJsonToPenalty(string json)
        {
            var serializerOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<Penalty>(json, serializerOptions) ?? new Penalty();
        }

        private List<Penalty> ConvertJsonToPenalties(string json)
        {
            var serializerOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<List<Penalty>>(json, serializerOptions) ?? new List<Penalty>();
        }

        #endregion

        #region methods

        public async Task<Penalty> Get(int id)
        {
            var response = await _service.Client.GetAsync($"api/Penalties/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToPenalty(data);
        }

        public async Task<List<Penalty>> GetAll()
        {
            var response = await _service.Client.GetAsync($"api/Penalties/GetAll");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToPenalties(data);
        }

        public async Task<List<Penalty>> GetByClassificationId(int classificationId)
        {
            var response = await _service.Client.GetAsync($"api/Penalties/GetByClassificationId/{classificationId}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToPenalties(data);
        }

        public async Task<Penalty> SaveUpdate(int userId, Penalty penalty)
        {
            var content = JsonContent.Create(penalty);
            var response = await _service.Client.PostAsync($"api/Penalties/SaveUpdate/{userId}", content);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToPenalty(data);
        }

        #endregion

    }
}
