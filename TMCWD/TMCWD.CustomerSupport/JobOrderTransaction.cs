using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TMCWD.Model.CustomerSupport;
using TMCWD.Services;

namespace TMCWD.CustomerSupport
{
    public class JobOrderTransaction
    {
        private readonly WebService _webService;
        public JobOrderTransaction(WebService webService) { _webService = webService; }

        public JobOrder ConvertJsonToJobOrder(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<JobOrder>(json, serializerOptions) ?? new();
        }
        
        public List<JobOrder> ConvertJsonToJobOrders(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<JobOrder>>(json, serializerOptions) ?? new();
        }

        public async Task<JobOrder> Get(int id, int requestId)
        {
            StringBuilder sb = new();

            if (id <= 0) sb.AppendLine("Id is required to get Job Order");
            if (requestId <= 0) sb.AppendLine("Job Order requires request");

            if (sb.ToString().Trim() != "") throw new Exception(sb.ToString());

            var response = await _webService.Client.GetAsync($"api/{requestId}/JobOrder/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToJobOrder(data);
        }

        public async Task<List<JobOrder>> GetAll(int requestId)
        {

            if (requestId <= 0) throw new Exception("Cannot identify request where job order belongs");

            var response = await _webService.Client.GetAsync($"api/{requestId}/JobOrder/GetAll");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToJobOrders(data);
        }

        public async Task<JobOrder> SaveUpdate(int userId, int requestId, JobOrder jobOrder)
        {

            StringBuilder sb = new();

            if (jobOrder == null) throw new Exception("Job order details are not provided");
            if (userId <= 0) sb.AppendLine("No currently logged in user");
            if (requestId <= 0) sb.AppendLine("Job Order must belong to a request which was not created");

            if (sb.ToString().Trim() != string.Empty) throw new Exception(sb.ToString());
            
            jobOrder.JobOrderNumber = "JO";

            var content = JsonContent.Create(jobOrder);
            var response = await _webService.Client.PostAsync($"api/{requestId}/JobOrder/SaveUpdate/{userId}", content);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToJobOrder(data);
        }

        public async Task<List<JobOrder>> GetByRequestId(int requestId)
        {
            var response = await _webService.Client.GetAsync($"api/{requestId}/JobOrder/GetByRequestId");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToJobOrders(data);
        }

        public async Task<JobOrder> GetByRequestDetailId(int requestId, int requestDetailId)
        {
            var response = await _webService.Client.GetAsync($"api/{requestId}/JobOrder/{requestDetailId}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToJobOrder(data);
        }

    }
}
