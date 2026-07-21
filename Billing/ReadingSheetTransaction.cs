using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using TMCWD.Model.Billing;
using TMCWD.Services;

namespace TMCWD.Billing
{
    public class ReadingSheetTransaction
    {

        #region fields

        private readonly WebService _webService;

        #endregion

        #region constructor

        public ReadingSheetTransaction(WebService webService)
        {
            _webService = webService;
        }

        #endregion

        #region private

        private ReadingSheet ConvertJsonToReadingSheet(string json)
        {
            var serialiazerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<ReadingSheet>(json, serialiazerOptions) ?? new();
        }

        private List<ReadingSheet> ConvertJsonToReadingSheets(string json)
        {
            var serialiazerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            return JsonSerializer.Deserialize<List<ReadingSheet>>(json, serialiazerOptions) ?? new();
        }

        #endregion

        #region methods

        public async Task<ReadingSheet> Get(int id)
        {
            var response = await _webService.Client.GetAsync($"api/ReadingSheet/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return null;

            return ConvertJsonToReadingSheet(data);
        }

        public async Task<List<ReadingSheet>> GetAll()
        {
            var response = await _webService.Client.GetAsync($"api/ReadingSheet/GetAll");
            var data = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return null;

            return ConvertJsonToReadingSheets(data);
        }

        public async Task<List<ReadingSheet>> GetByAssignedTo(int assignedTo)
        {
            var response = await _webService.Client.GetAsync($"api/ReadingSheet/GetByAssignedTo/{assignedTo}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToReadingSheets(data);
        }

        public async Task<List<ReadingSheet>> GetByZoneBookAndAssignedTo(int zone, int book, int assignedTo)
        {
            var response = await _webService.Client.GetAsync($"api/ReadingSheet/GetByZoneBookAndAssignedTo/{zone}/{book}/{assignedTo}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToReadingSheets(data);
        }

        public async Task<List<ReadingSheet>> GetByZoneAndBook(int zone, int book)
        {
            var response = await _webService.Client.GetAsync($"api/ReadingSheet/GetByZoneAndBook/{zone}/{book}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToReadingSheets(data);
        }

        public async Task<ReadingSheet> GetByBillingDate(DateTime billingDate)
        {
            var response = await _webService.Client.GetAsync($"api/ReadingSheet/GetByBillingDate/{billingDate}");
            var data = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return null;

            return ConvertJsonToReadingSheet(data);
        }

        public async Task<ReadingSheet> GetCurrentByAssignedTo(int assignedTo)
        {
            var response = await _webService.Client.GetAsync($"api/ReadingSheet/GetCurrentByAssignedTo/{assignedTo}");
            var data = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return null;

            return ConvertJsonToReadingSheet(data);
        }

        public async Task<ReadingSheet> SaveUpdate(int userId, ReadingSheet readingSheet) 
        {
            var content = JsonContent.Create(readingSheet);
            var response = await _webService.Client.PostAsync($"api/ReadingSheet/SaveUpdate/{userId}", content);
            var data = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToReadingSheet(data);
        }

        #endregion

    }
}
