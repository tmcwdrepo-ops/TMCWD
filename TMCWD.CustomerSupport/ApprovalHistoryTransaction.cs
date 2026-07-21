using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TMCWD.Model.CustomerSupport;
using TMCWD.Services;

namespace TMCWD.CustomerSupport
{
    public class ApprovalHistoryTransaction
    {

        private readonly WebService _webService;

        public ApprovalHistoryTransaction(WebService webService)
        {
            _webService = webService;
        }

        public ApprovalHistory ConvertJsonToApprovalHistory(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<ApprovalHistory>(json, serializerOptions) ?? new();
        }

        public List<ApprovalHistory> ConvertJsonToApprovalHistories(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<ApprovalHistory>>(json, serializerOptions) ?? new();
        }

        public async Task<List<ApprovalHistory>> GetAll(int jobOrderId)
        {
            if (jobOrderId <= 0) throw new Exception("Job order id is required to get history");

            var response = await _webService.Client.GetAsync($"api/{jobOrderId}/ApprovalHistory/GetAll");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;

            return ConvertJsonToApprovalHistories(data);
        }

        public async Task<ApprovalHistory> Save(int userId, int jobOrderId, ApprovalHistory history)
        {
            StringBuilder sb = new();

            if (history == null) throw new Exception("Approval history details is empty");
            if (jobOrderId <= 0) sb.AppendLine("Job order is not specified for this approval history");
            if (string.IsNullOrEmpty(history.Details.Trim())) sb.AppendLine("History detail is needed");

            if (sb.ToString().Trim() == "") throw new Exception(sb.ToString());

            var content = JsonContent.Create(history);

            var response = await _webService.Client.PostAsync($"api/{jobOrderId}/ApprovalHistory/{userId}", content);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToApprovalHistory(data);
        }

    }
}
