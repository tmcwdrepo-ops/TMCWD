using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TMCWD.Model.CustomerSupport;
using TMCWD.Services;

namespace TMCWD.CustomerSupport
{
    public class RequestFileTransaction
    {

        private readonly WebService _service;

        public RequestFileTransaction(WebService service)
        {
            _service = service;
        }

        public RequestFile ConvertJsonToRequestFile(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<RequestFile>(json, serializerOptions) ?? new();
        }

        public List<RequestFile> ConvertJsonToRequestFiles(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<RequestFile>>(json, serializerOptions) ?? new();
        }

        public async Task<RequestFile> Get(int id)
        {
            var response = await _service.Client.GetAsync($"api/RequestFile/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToRequestFile(data);
        }

        public async Task<List<RequestFile>> GetAll(int jobOrderId)
        {
            var response = await _service.Client.GetAsync($"api/RequestFile/GetAll/{jobOrderId}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToRequestFiles(data);
        }

        public async Task<RequestFile> SaveUpdate(int userId, int jobOrderId, RequestFile file)
        {
            var content = JsonContent.Create(file);
            var response = await _service.Client.PostAsync($"api/RequestFile/SaveUpdate/{userId}/{jobOrderId}", content);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToRequestFile(data);
        }

        public async Task<List<RequestFile>> SaveRange(List<RequestFile> files)
        {
            var content = JsonContent.Create(files);
            var response = await _service.Client.PostAsync("api/RequestFile/SaveRange", content);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToRequestFiles(data);
        }

    }
}
