using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TMCWD.Model.Administrator;
using TMCWD.Services;

namespace TMCWD.Administration
{
    public class OtherFeeTypeTransaction
    {

        private readonly WebService _webService;

        public OtherFeeTypeTransaction(WebService webService)
        { 
            _webService = webService;
        }

        public OtherFeeType ConvertJsonToOtherFeeType(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<OtherFeeType>(json, serializerOptions) ?? new();
        }

        public List<OtherFeeType> ConvertJsonToOtherFeeTypes(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<OtherFeeType>>(json, serializerOptions) ?? new();
        }

        public async Task<OtherFeeType> Get(int id)
        {
            var response = await _webService.Client.GetAsync($"api/OtherFeeType/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToOtherFeeType(data);
        }

        public async Task<List<OtherFeeType>> GetAll()
        {
            var response = await _webService.Client.GetAsync("api/OtherFeeType/GetAll");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToOtherFeeTypes(data);
        }

        public async Task<List<OtherFeeType>> GetByName(string name)
        {

            if (String.IsNullOrEmpty(name.Trim())) throw new Exception("Name is required to get other fee type by name");
            var response = await _webService.Client.GetAsync($"api/OtherFeeType/GetByName/{name}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToOtherFeeTypes(data);
        }

        public async Task<OtherFeeType> SaveUpdate(int userId, OtherFeeType otherFeeType)
        {
            if (String.IsNullOrEmpty(otherFeeType.Name.Trim())) throw new Exception("Name is required to save other fee type");
            var content = JsonContent.Create(otherFeeType);
            var response = await _webService.Client.PostAsync($"api/OtherFeeType/SaveUpdate/{userId}", content);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToOtherFeeType(data);
        }

        public async Task<bool> Delete(int id)
        {
            if (id <= 0) throw new Exception("Other fee type id is required to delete.");
            var response = await _webService.Client.DeleteAsync($"api/OtherFeeType/Delete/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if(!response.IsSuccessStatusCode) return false;
            bool.TryParse(data, out bool isSuccess);
            return isSuccess;
        }

    }
}
