using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TMCWD.Model.Engineering;
using TMCWD.Model.Interfaces;
using TMCWD.Services;
using TMCWD.Utility.Generic;

namespace TMCWD.Engineering
{
    public class InventoryTransaction : TransactionBase
    {

        #region fields

        private WebService _webService;

        #endregion

        #region constructors

        public InventoryTransaction(WebService webService) { _webService = webService; }

        #endregion

        #region public methods

        public Inventory ConvertJsonToInventory(string json)
        {
            var serializeOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<Inventory>(json, serializeOptions) ?? new Inventory();
        }

        public List<Inventory> ConvertJsonToInventoryItems(string json)
        {
            var serializeOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<Inventory>>(json, serializeOptions) ?? new List<Inventory>();
        }

        public async Task<Inventory> SaveUpdate(int userId, Inventory inventory)
        {

            if (inventory == null) throw new Exception("Inventory data is null");

            StringBuilder sb = new();

            if (inventory.Division <= 0) sb.AppendLine("Please specify which division");
            if (String.IsNullOrEmpty(inventory.UOM.Trim())) sb.AppendLine("Please specify the item unit");
            if (String.IsNullOrEmpty(inventory.Name.Trim())) sb.AppendLine("Please specify the item name");
            if (inventory.UnitCost <= 0) sb.AppendLine("Please specify the unit cost");

            if(!String.IsNullOrEmpty(sb.ToString().Trim())) throw new Exception(sb.ToString());
             
            var content = JsonContent.Create(inventory);

            var response = await _webService.Client.PostAsync($"api/Inventory/SaveUpdate/{userId}", content);

            var data = await response.Content.ReadAsStringAsync();

            if(!response.IsSuccessStatusCode) throw new Exception(data);

            return ConvertJsonToInventory(data);
        }

        public async Task<Inventory> Get(int id)
        {

            if (id <= 0) throw new Exception("Id is required to get inventory item");

            var response = await _webService.Client.GetAsync($"api/Inventory/Get/{id}");

            var data = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return null;

            return ConvertJsonToInventory(data);
        }

        public async Task<List<Inventory>> GetAll()
        {

            var response = await _webService.Client.GetAsync($"api/Inventory/GetAll");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToInventoryItems(data);
        }

        public async Task<List<Inventory>> GetByName(string name)
        {
            var response = await _webService.Client.GetAsync($"api/Inventory/GetByName/{name}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToInventoryItems(data);
        }

        #endregion

    }
}
