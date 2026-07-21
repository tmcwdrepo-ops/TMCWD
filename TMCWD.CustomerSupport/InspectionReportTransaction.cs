using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TMCWD.Model.CustomerSupport;
using TMCWD.Model.Interfaces;
using TMCWD.Services;
using TMCWD.Utility.Generic;

namespace TMCWD.CustomerSupport
{
    public class InspectionReportTransaction : TransactionBase
    {

        #region fields
        private readonly WebService _webService;
        #endregion

        #region constructors
        public InspectionReportTransaction(WebService webService) { _webService = webService; }
        #endregion

        #region public methods

        public InspectionReport ConvertJsonToInspectionReport(string json)
        {
            var serializeOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<InspectionReport>(json, serializeOptions) ?? new InspectionReport();
        }

        public List<InspectionReport> ConvertJsonToInspectionReports(string json)
        {
            var serializeOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<InspectionReport>>(json, serializeOptions) ?? new List<InspectionReport>();
        }

        public async Task<InspectionReport> SaveUpdate(int userId, InspectionReport report)
        {
            StringBuilder sb = new();
            if (report == null) throw new Exception("Inspection report data is empty");
            if (report.RequestId <= 0) sb.AppendLine("Request is required before creating inspection report");
            if (String.IsNullOrEmpty(report.Details.Trim())) sb.AppendLine("Inspection report details is not provided");

            if (String.IsNullOrEmpty(sb.ToString().Trim())) throw new Exception(sb.ToString());

            var content = JsonContent.Create(report);

            var response = await _webService.Client.PostAsync($"api/InspectionReport/SaveUpdate/{userId}", content);
            var data = await response.Content.ReadAsStringAsync();
            if(!response.IsSuccessStatusCode) throw new Exception(data);

            return ConvertJsonToInspectionReport(data);
        }

        public async Task<InspectionReport> Get(int id)
        {

            if (id <= 0) throw new Exception("Inspection report id is required to get details.");

            var response = await _webService.Client.GetAsync($"api/InspectionReport/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if(!response.IsSuccessStatusCode) throw new Exception(data);
            
            return ConvertJsonToInspectionReport(data);
        }

        public async Task<List<InspectionReport>> GetByRequestId(int requestId)
        {
            if (requestId <= 0) throw new Exception("Request id is required to get inspection report");
            
            var response = await _webService.Client.GetAsync($"api/InspectionReport/GetByRequestId/{requestId}");
            var data = await response.Content.ReadAsStringAsync();
            if(!response.IsSuccessStatusCode) throw new Exception(data);

            return ConvertJsonToInspectionReports(data);
        }

        #endregion

    }
}
