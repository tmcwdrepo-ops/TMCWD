using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TMCWD.Model.CustomerSupport;
using TMCWD.Services;

namespace TMCWD.CustomerSupport
{
    public class FindingTransaction
    {

        private readonly WebService _webService;

        public FindingTransaction(WebService webService) { _webService = webService; }

        public Finding ConvertJsonToFinding(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<Finding>(json, serializerOptions) ?? new();
        }

        public List<Finding> ConvertJsonToFindings(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<Finding>>(json, serializerOptions) ?? new();
        }

        public async Task<Finding> Get(int jobOrderId, int id)
        {
            StringBuilder sb = new();

            if (jobOrderId == 0) sb.AppendLine("Request id is required to get finding");
            if (id == 0) sb.AppendLine("Id is required to get finding");
            var response = await _webService.Client.GetAsync($"api/{jobOrderId}/Finding/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToFinding(data);
        }

        public async Task<List<Finding>> GetAll(int jobOrderId)
        {

            if (jobOrderId == 0) throw new Exception("Request id is required to get all findings");
            var response = await _webService.Client.GetAsync($"api/{jobOrderId}/Finding/GetAll");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToFindings(data);
        }

        public async Task<Finding> SaveUpdate(int userId, int jobOrderId, Finding finding)
        {

            StringBuilder sb = new();

            if (finding == null) throw new Exception("No finding data");
            if (String.IsNullOrEmpty(finding.Detail.Trim())) sb.AppendLine("Finding detail is required.");
            if (jobOrderId == 0) sb.AppendLine("Request id is required to save finding");
            if(userId == 0) sb.AppendLine("No currently logged in user");

            if (sb.ToString().Trim() != String.Empty) throw new Exception(sb.ToString());

            var content = JsonContent.Create(finding);
            var response = await _webService.Client.PostAsync($"api/{jobOrderId}/Finding/SaveUpdate/{userId}", content);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToFinding(data);
        }

    }
}
