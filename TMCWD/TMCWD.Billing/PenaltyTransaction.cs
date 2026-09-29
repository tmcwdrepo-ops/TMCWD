using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TMCWD.Model.Billing;
using TMCWD.Services;

namespace TMCWD.Billing
{
    public class PenaltyTransaction
    {

        #region fields
        private readonly WebService _service;
        #endregion

        #region constructors
        public PenaltyTransaction(WebService service)
        {
            _service = service;
        }
        #endregion

        #region private

        private Penalty ConvertJsonToPenalty(string json)
        {
            var serializerOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<Penalty>(json, serializerOptions) ?? new Penalty();
        }

        private List<Penalty> ConvertJsonToPenalties(string json)
        {
            var serializerOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<List<Penalty>>(json, serializerOptions) ?? new List<Penalty>();
        }

        #endregion

        #region methods

        public async Task<Penalty> Get(int id)
        {
            var response = await _service.Client.GetAsync($"api/Penalty/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToPenalty(data);
        }

        public async Task<List<Penalty>> GetAll()
        {
            var response = await _service.Client.GetAsync($"api/Penalty/GetAll");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToPenalties(data);
        }

        public async Task<List<Penalty>> GetByReference(string reference)
        {
            var response = await _service.Client.GetAsync($"api/Penalty/GetByReference/{reference}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToPenalties(data);
        }

        public async Task<Penalty> SaveUpdate(int userId, Penalty penalty)
        {
            var content = JsonContent.Create(penalty);
            var response = await _service.Client.PostAsync($"api/Penalty/SaveUpdate/{userId}", content);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToPenalty(data);
        }

        public async Task<int> SubmitWaiverRequests(int userId, SubmitPenaltyWaiverRequest request)
        {
            using var response = await _service.Client.PostAsync($"api/Penalty/SubmitWaiver/{userId}", JsonContent.Create(request));
            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                throw new InvalidOperationException(await response.Content.ReadAsStringAsync());
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<int>();
        }

        public async Task<List<PenaltyWaiverRequestRecord>> GetPendingWaiverRequests(int userId)
        {
            using var response = await _service.Client.GetAsync($"api/Penalty/PendingWaivers/{userId}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<PenaltyWaiverRequestRecord>>() ?? new();
        }

        public async Task<int> ReviewWaiverRequest(int userId, ReviewPenaltyWaiverRequest request)
        {
            using var response = await _service.Client.PostAsync($"api/Penalty/ReviewWaiver/{userId}", JsonContent.Create(request));
            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                throw new InvalidOperationException(await response.Content.ReadAsStringAsync());
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<int>();
        }

        public async Task<List<PenaltyChargeEntry>> GetChargeEntries(DateTime? billingPeriod)
        {
            var query = billingPeriod.HasValue ? $"?billingPeriod={billingPeriod:yyyy-MM-dd}" : "";
            using var response = await _service.Client.GetAsync("api/Penalty/ChargeEntries" + query);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<PenaltyChargeEntry>>() ?? new();
        }

        public async Task<PostPenaltyChargesResult> PostCharges(int userId, PostPenaltyChargesRequest request)
        {
            using var response = await _service.Client.PostAsync($"api/Penalty/PostCharges/{userId}", JsonContent.Create(request));
            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                throw new InvalidOperationException("A selected bill is no longer eligible. Reload entries before posting again.");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<PostPenaltyChargesResult>()
                ?? throw new HttpRequestException("Penalty posting was not confirmed.");
        }

        #endregion

    }
}
