using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TMCWD.Model.CustomerSupport;
using TMCWD.Model.Interfaces;
using TMCWD.Services;
using TMCWD.Utility.Generic;

namespace TMCWD.CustomerSupport
{
    public class RequestTransaction : TransactionBase
    {

        #region fields

        private readonly WebService _webService;

        #endregion

        #region constructors

        public RequestTransaction(WebService webService) { _webService = webService;  }

        #endregion

        #region public methods

        public Request ConvertJsonToRequest(string json)
        {
            var serializeOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<Request>(json, serializeOptions) ?? new Request();
        }

        public List<Request> ConvertJsonToRequests(string json)
        {
            var serializeOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive  = true };
            return JsonSerializer.Deserialize<List<Request>>(json, serializeOptions) ?? new List<Request>();
        }

        public RequestDetail ConvertJsonToRequestDetail(string json)
        {
            var serializeOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<RequestDetail>(json, serializeOptions) ?? new RequestDetail();
        }

        public List<RequestDetail> ConvertJsonToRequestDetails(string json)
        {
            var serializeOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<RequestDetail>>(json, serializeOptions) ?? new List<RequestDetail>();
        }

        public async Task<Request> SaveUpdate(int userId,  Request request)
        {
            StringBuilder sb = new();
            if (request == null) throw new Exception("Request data has not been supplied to create or update a request");
            if (request.AccountId <= 0) sb.AppendLine("Creating or updating request requires account");
            if (request.CustomerId <= 0) sb.AppendLine("Creating or updating request requires customer with account");

            if(!String.IsNullOrEmpty(sb.ToString().Trim())) throw new Exception(sb.ToString());

            if (request.Id <= 0) request.ControlNumber = "TKT";

            var content = JsonContent.Create(request);
            var response = await _webService.Client.PostAsync($"api/Request/SaveUpdate/{userId}", content);

            var data = await response.Content.ReadAsStringAsync();
            if(!response.IsSuccessStatusCode) throw new Exception(data);

            return ConvertJsonToRequest(data);
        }

        public async Task<Request> Get(int id)
        {

            if (id <= 0) throw new Exception("Getting request by id requires request id");

            var response = await _webService.Client.GetAsync($"api/Request/Get/{id}");

            var data = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return null;

            return ConvertJsonToRequest(data);
        }

        public async Task<List<Request>> GetRequests()
        {

            var response = await _webService.Client.GetAsync($"api/Request/GetRequests");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToRequests(data);
        }

        public async Task<List<Request>> GetByUserId(int userId)
        {
            if (userId <= 0) throw new Exception("User id is required");

            var response = await _webService.Client.GetAsync($"api/Request/GetByUserId/{userId}");

            var data = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return null;

            return ConvertJsonToRequests(data);
        }

        public async Task<List<Request>> GetByCustomerId(int customerId)
        {

            if (customerId <= 0) throw new Exception("Customer id is required");

            var response = await _webService.Client.GetAsync($"api/Request/GetByCustomerId/{customerId}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToRequests(data);
        }

        public async Task<RequestDetail> SaveUpdateRequestDetail(int userId, int requestId, RequestDetail detail)
        {

            if (detail == null) throw new Exception("Request details is empty");

            StringBuilder sb = new();

            if (string.IsNullOrEmpty(detail.InspectionTypeDetail.Trim())) sb.AppendLine("Inspection detail is not specified");
            if (detail.RequestTypeId <= 0) sb.AppendLine("Select at least one inspection type to be performed");
            if (detail.RequestId <= 0) sb.AppendLine("Request for detail is not found");

            if(String.IsNullOrEmpty(sb.ToString().Trim())) throw new Exception(sb.ToString());

            var content = JsonContent.Create(detail);
            var response = await _webService.Client.PostAsync($"api/RequestDetail/{requestId}/SaveUpdate/{userId}", content);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new Exception(data);
            
            return ConvertJsonToRequestDetail(data);
        }

        public async Task<IEnumerable<RequestDetail>> SaveMulipleRequestDetail(int userId, int requestId, List<RequestDetail> details)
        {
            StringBuilder sb = new();
            if (details == null || !details.Any()) throw new Exception("Request details is empty");

            var emptyDetailType = details.Where(x => string.IsNullOrEmpty(x.InspectionTypeDetail.Trim()));
            var noInspectionType = details.Where(x => x.RequestTypeId <= 0).FirstOrDefault();
            var noRequestId = details.Where(x => x.RequestId <= 0).FirstOrDefault();

            if (emptyDetailType == null || !emptyDetailType.Any()) sb.AppendLine("One of the request detail is missing the required inspection type detail");
            if (noInspectionType != null) sb.AppendLine("One of the request detail is missing the required inspection type");
            if (noRequestId != null) sb.AppendLine("One of the request detail is missing the request");

            if(!String.IsNullOrEmpty(sb.ToString().Trim())) throw new Exception(sb.ToString());

            var content = JsonContent.Create(details);
            var response = await _webService.Client.PostAsync($"api/{requestId}/RequestDetail/SaveMultiple/{userId}", content);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new Exception(data);
            var returnedDetails = ConvertJsonToRequestDetails(data);
            return returnedDetails;
        }

        public async Task<RequestDetail> GetRequestDetail(int id, int requestId)
        {
            var response = await _webService.Client.GetAsync($"api/{requestId}/RequestDetail/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return this.ConvertJsonToRequestDetail(data);
        }

        public async Task<List<RequestDetail>> GetRequestDetailByRequestId(int requestId)
        {
            var response = await _webService.Client.GetAsync($"api/{requestId}/RequestDetail/GetByRequestId");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return this.ConvertJsonToRequestDetails(data);
        }

        public async Task<bool> DeleteRequestDetail(int id)
        {

            if (id <= 0) throw new Exception("Request detail id is required to delete request detail item");

            var response = await _webService.Client.DeleteAsync($"api/RequestDetail/Delete/{id}");

            var data = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) throw new Exception(data);

            return data.ToLower() == "true";
        }

        public async Task<bool> DeleteRequestDetails(int requestId)
        {
            if (requestId <= 0) throw new Exception("Request id is required to delete request details");

            var response = await _webService.Client.DeleteAsync($"api/RequestDetail/DeleteDetails/{requestId}");

            var data = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) throw new Exception(data);

            return data.ToLower() == "true";
        }

        #endregion

    }

}
