using System;
using System.Net.Http.Json;
using TMCWD.Model.Billing;
using TMCWD.Services;

namespace TMCWD.Billing
{
    public class OtherChargeTransaction
    {

        #region fields

        private readonly WebService _service;

        #endregion

        #region constructors

        public OtherChargeTransaction(WebService service)
        {
            _service = service;
        }

        #endregion

        #region private

        private OtherCharge ConvertJsonToOtherCharge(string json)
        {
            var serializerOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            return System.Text.Json.JsonSerializer.Deserialize<OtherCharge>(json, serializerOptions) ?? new OtherCharge();
        }

        private List<OtherCharge> ConvertJsonToOtherCharges(string json)
        {
            var serializerOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            return System.Text.Json.JsonSerializer.Deserialize<List<OtherCharge>>(json, serializerOptions) ?? new List<OtherCharge>();
        }

        #endregion

        #region methods

        public async Task<OtherCharge> Get(int id)
        {
            var response = await _service.Client.GetAsync($"api/OtherCharge/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToOtherCharge(data);
        }

        public async Task<List<OtherCharge>> GetAll()
        {
            var response = await _service.Client.GetAsync($"api/OtherCharge/GetAll");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return new List<OtherCharge>();
            return ConvertJsonToOtherCharges(data);
        }

        public async Task<List<OtherCharge>> GetByReference(string reference)
        {
            var response = await _service.Client.GetAsync(
                $"api/OtherCharge/GetByReference/{Uri.EscapeDataString(reference)}"
            );

            var data = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return new List<OtherCharge>();
            response.EnsureSuccessStatusCode();
            return ConvertJsonToOtherCharges(data);
        }

        public async Task<List<OtherCharge>> GetByClassificationId(int classificationId)
        {
            var response = await _service.Client.GetAsync($"api/OtherCharge/GetByClassificationId/{classificationId}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return new List<OtherCharge>();
            return ConvertJsonToOtherCharges(data);
        }

        public async Task<OtherCharge> SaveUpdate(int userId, OtherCharge otherCharge)
        {
            var content = JsonContent.Create(otherCharge);
            var response = await _service.Client.PostAsync($"api/OtherCharge/SaveUpdate/{userId}", content);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                    throw new HttpRequestException("The charge changed. Reload and try again.", null, response.StatusCode);
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    using var json = System.Text.Json.JsonDocument.Parse(data);
                    throw new InvalidOperationException(json.RootElement.GetProperty("message").GetString());
                }
                throw new HttpRequestException("The charge could not be saved.", null, response.StatusCode);
            }
            return ConvertJsonToOtherCharge(data);
        }

        public async Task<OtherCharge> Deactivate(int id, int userId)
        {
            var response = await _service.Client.PutAsync($"api/OtherCharge/Deactivate/{id}/{userId}", null);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToOtherCharge(data);
        }

        public async Task<int> PostToCollection(
            int userId,
            TMCWD.Model.Billing.Requests.PostOtherChargesRequest request)
        {
            using var response = await _service.Client.PostAsJsonAsync(
                $"api/OtherCharge/PostToCollection/{userId}", request);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    using var json = System.Text.Json.JsonDocument.Parse(data);
                    if (json.RootElement.TryGetProperty("message", out var message))
                        throw new InvalidOperationException(message.GetString());
                }
                catch (System.Text.Json.JsonException) { }
                throw new HttpRequestException("The charge posting was not confirmed.");
            }

            using var result = System.Text.Json.JsonDocument.Parse(data);
            return result.RootElement.GetProperty("postedCount").GetInt32();
        }

        public async Task<(bool Success, string Message, string InvoiceNumber, int StatusCode)> ProcessPayment(
            int userId,
            TMCWD.Model.Billing.Requests.OtherChargePaymentRequest request)
        {
            var response = await _service.Client.PostAsJsonAsync(
                $"api/OtherCharge/ProcessPayment/{userId}", request);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                body.TryGetProperty("invoiceNumber", out var invEl);
                return (true, string.Empty, invEl.GetString() ?? throw new InvalidOperationException("Payment reference was not returned."), 200);
            }

            var data = await response.Content.ReadAsStringAsync();
            try
            {
                using var json = System.Text.Json.JsonDocument.Parse(data);
                if (json.RootElement.TryGetProperty("message", out var message))
                    return (false, message.GetString() ?? "Payment could not be completed.", string.Empty, (int)response.StatusCode);
            }
            catch (System.Text.Json.JsonException) { }

            return (false,
                $"Other-charge payment service returned {(int)response.StatusCode} ({response.ReasonPhrase}).",
                string.Empty, (int)response.StatusCode);
        }

        #endregion

    }
}
