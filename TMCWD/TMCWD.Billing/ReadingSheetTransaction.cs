using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Xml;
using TMCWD.Model.Billing;
using TMCWD.Services;
using TMCWD.Model.Billing.Responses;

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

        public async Task<List<ReadingSheet>> GetByZoneAndBook(int zone, int book)
        {
            var response = await _webService.Client.GetAsync($"api/ReadingSheet/GetByZoneAndBook/{zone}/{book}");
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

        public async Task<List<ReadingSheet>> GetByBillingDate(int zone, int book, DateTime billingDate)
        {
            var response = await _webService.Client.GetAsync($"api/ReadingSheet/GetByBillingDate/{zone}/{book}/{billingDate}");
            var data = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return null;

            return ConvertJsonToReadingSheets(data);
        }

        public async Task<ReadingSheet> GetByBillingDateAndAssignedTo(int zone, int book, DateTime billingDate, int assignedTo)
        {
            var response = await _webService.Client.GetAsync($"api/ReadingSheet/GetByBillingDateAndAssignedTo/{zone}/{book}/{billingDate}/{assignedTo}");
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

        public async Task<List<ReadingSheet>> GetByZoneBookId(int zoneBookId)
        {
            var response = await _webService.Client.GetAsync($"api/ReadingSheet/GetByZoneBookId/{zoneBookId}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToReadingSheets(data);
        }

        public async Task<ReadingSheet> GetByBillingPeriodStart(int zone, int book, DateTime billingPeriodStart)
        {
            var response = await _webService.Client.GetAsync($"api/ReadingSheet/GetByBillingPeriodStart/{zone}/{book}/{billingPeriodStart}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToReadingSheet(data);
        }

        public async Task<ReadingSheet> GetCurrentByAssignedTo(int zone, int book, int assignedTo)
        {
            var response = await _webService.Client.GetAsync($"api/ReadingSheet/GetCurrentByAssignedTo/{zone}/{book}/{assignedTo}");
            var data = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return null;

            return ConvertJsonToReadingSheet(data);
        }

        public async Task<List<ReadingSheet>> GetRangeReadingSheets(List<int> ids)
        {
            string queryParam = string.Join("&", ids.Select(x => $"ids={x}"));
            var response = await _webService.Client.GetAsync($"api/ReadingSheet/GetRangeReadingSheets?{queryParam}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToReadingSheets(data);
        }

        public async Task<List<ReadingSheet>> UpdateReadingSheetsStatus(List<int> ids, int userId, ReadingStatus status)
        {
            string queryParam = string.Join("&", ids.Select(x => $"ids={x}"));
            var response = await _webService.Client.PatchAsync($"api/ReadingSheet/UpdateReadingSheetsStatus/{userId}/{(int)status}?{queryParam}", null);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToReadingSheets(data);
        }


        public async Task<List<ReadingSheetAccountDto>> GetReadingSheetAccounts(int readingSheetId)
        {
            var response = await _webService.Client.GetAsync($"api/ReadingSheet/GetReadingSheetAccounts?readingSheetId={readingSheetId}");
            var data = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return null;

            var serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<List<ReadingSheetAccountDto>>(data, serializerOptions) ?? new();
        }
        #endregion

    }
}
