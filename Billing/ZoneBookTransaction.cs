using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using TMCWD.Model.Billing;
using TMCWD.Services;

namespace TMCWD.Billing
{
    public class ZoneBookTransaction
    {

        #region fields

        private readonly WebService _service;

        #endregion

        #region constructors

        public ZoneBookTransaction(WebService service)
        {
            _service = service;
        }

        #endregion

        #region methods

        public ZoneBook ConvertJsonToZoneBook(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<ZoneBook>(json, serializerOptions) ?? new();
        }

        public List<ZoneBook> ConvertJsonToZoneBooks(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<ZoneBook>>(json, serializerOptions) ?? new();
        }

        public async Task<ZoneBook> Get(string id)
        {
            var response = await _service.Client.GetAsync($"api/ZoneBook/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToZoneBook(data);
        }

        public async Task<List<ZoneBook>> GetAll()
        {
            var response = await _service.Client.GetAsync($"api/ZoneBook/GetAll");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToZoneBooks(data);
        }

        public async Task<List<ZoneBook>> GetByZone(int zone)
        {
            var response = await _service.Client.GetAsync($"api/ZoneBook/GetByZone/{zone}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToZoneBooks(data);
        }

        public async Task<List<ZoneBook>> GetZones()
        {
            var response = await _service.Client.GetAsync("api/ZoneBook/GetZones");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToZoneBooks(data);
        }

        public async Task<List<ZoneBook>> GetBooksByZone(int zone)
        {
            var response = await _service.Client.GetAsync($"api/ZoneBook/GetBooksByZone/{zone}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToZoneBooks(data);
        }

        public async Task<ZoneBook> GetByZoneAndBook(int zone, int book)
        {
            var response = await _service.Client.GetAsync($"api/ZoneBook/GetByZoneAndBook/{zone}/{book}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToZoneBook(data);
        }

        public async Task<List<ZoneBook>> GetByWeek(int week)
        {
            var response = await _service.Client.GetAsync($"api/ZoneBook/GetByWeek/{week}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToZoneBooks(data);
        }

        #endregion

    }
}
