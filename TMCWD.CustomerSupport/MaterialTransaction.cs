using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TMCWD.Model.CustomerSupport;
using TMCWD.Services;

namespace TMCWD.CustomerSupport
{
    public class MaterialTransaction
    {
        private readonly WebService _webService;

        public MaterialTransaction(WebService webService) { _webService = webService; }

        public Material ConvertJsonToMaterial(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<Material>(json, serializerOptions) ?? new();
        }

        public List<Material> ConvertJsonToMaterials(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<Material>>(json, serializerOptions) ?? new();
        }

        public async Task<Material> Get(int requestId, int id)
        {
            var response = await _webService.Client.GetAsync($"api/{requestId}/Material/{id}");

            var data = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return null;

            return ConvertJsonToMaterial(data);
        }

        public async Task<List<Material>> GetAll() 
        {
            var response = await _webService.Client.GetAsync("api/0/Material/GetAll");

            var data = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return null;

            return ConvertJsonToMaterials(data);
        }

        public async Task<List<Material>> GetByJobOrderId(int jobOrderId)
        {
            var response = await _webService.Client.GetAsync($"api/{jobOrderId}/Material/GetByJobOrderId");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToMaterials(data);
        }

        public async Task<Material> SaveUpdate(int userId, int jobOrderId, Material material)
        {
            var content = JsonContent.Create(material);

            var response = await _webService.Client.PostAsync($"api/{jobOrderId}/Material/SaveUpdate/{userId}", content);

            var data = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return null;

            return ConvertJsonToMaterial(data);
        }

        public async Task<Material> UpdateQuantityOrNewUnitCost(int userId, int requestId, int id, int quantity, float unitCost)
        {
            Material material = new()
            {
                Id = id,
                RequestedQuantity = quantity
                //NewUnitCost = unitCost
            };

            var content = JsonContent.Create(material);

            var response = await _webService.Client.PutAsync($"api/{requestId}/Material/UpdateQuantityOrNewUnitCost/{userId}", content);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToMaterial(data);
        }

        public async Task<bool> DeleteMaterial(int jobOrderId, int id)
        {
            var response = await _webService.Client.DeleteAsync($"api/{jobOrderId}/Material/Delete/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return false;
            return data == "true";
        }

    }

}
