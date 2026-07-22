using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TMCWD.Model.Billing;
using TMCWD.Services;

namespace TMCWD.Billing
{
    public class ReadingSheetTemplateTransaction
    {

        #region fields

        private readonly WebService _service;

        #endregion

        #region constructors

        public ReadingSheetTemplateTransaction(WebService service)
        {
            _service = service;
        }

        #endregion

        #region methods

        public ReadingSheetTemplate ConvertJsonToReadingSheetTemplate(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<ReadingSheetTemplate>(json, serializerOptions) ?? new();
        }

        public List<ReadingSheetTemplate> ConvertJsonToReadingSheetTemplates(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<ReadingSheetTemplate>>(json, serializerOptions) ?? new();
        }

        public async Task<ReadingSheetTemplate> Get(int id)
        {
            var response = await _service.Client.GetAsync($"api/ReadingSheetTemplate/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToReadingSheetTemplate(data);
        }

        public async Task<List<ReadingSheetTemplate>> GetAll()
        {
            var response = await _service.Client.GetAsync("api/ReadingSheetTemplate/GetAll");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToReadingSheetTemplates(data);
        }

        public async Task<List<ReadingSheetTemplate>> GetByUser(int userId)
        {
            var response = await _service.Client.GetAsync($"api/ReadingSheetTemplate/GetByUser/{userId}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToReadingSheetTemplates(data);
        }

        public async Task<ReadingSheetTemplate> SaveUpdate(int userId, ReadingSheetTemplate template)
        {
            var content = JsonContent.Create(template);
            var response = await _service.Client.PostAsync($"api/ReadingSheetTemplate/SaveUpdate/{userId}", content);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToReadingSheetTemplate(data);
        }

        #endregion

    }
}
