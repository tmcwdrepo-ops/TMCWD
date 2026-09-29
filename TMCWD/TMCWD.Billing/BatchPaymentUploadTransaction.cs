using System.Net.Http.Json;
using System.Text.Json;
using TMCWD.Services;

namespace TMCWD.Billing
{
    /// <summary>
    /// Thin HTTP client over the TMCWD.Data BatchPaymentUpload API.
    /// Shape mirrors the entity: { id, filename, channel, status, rowCount, createdBy, dateCreated }.
    /// </summary>
    public class BatchPaymentUploadTransaction
    {
        private readonly WebService _service;

        public BatchPaymentUploadTransaction(WebService service)
        {
            _service = service;
        }

        // ── DTOs (anonymous-object friendly, no TMCWD.Data reference needed) ───

        public record BatchUploadRecord(
            long     Id,
            string   Filename,
            string   Channel,
            string   Status,
            int      RowCount,
            long     CreatedBy,
            DateTime DateCreated);

        private static readonly JsonSerializerOptions _opts =
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // ── Queries ──────────────────────────────────────────────────────────────

        public async Task<List<BatchUploadRecord>> GetByDateRange(DateTime from, DateTime to)
        {
            var url = $"api/BatchPaymentUpload/GetByDateRange" +
                      $"?from={Uri.EscapeDataString(from.ToString("yyyy-MM-dd"))}" +
                      $"&to={Uri.EscapeDataString(to.ToString("yyyy-MM-dd"))}";
            var response = await _service.Client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return new();
            return await response.Content.ReadFromJsonAsync<List<BatchUploadRecord>>(_opts) ?? new();
        }

        public async Task<BatchUploadRecord?> Get(long id)
        {
            var response = await _service.Client.GetAsync($"api/BatchPaymentUpload/Get/{id}");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<BatchUploadRecord>(_opts);
        }

        // ── Mutations ────────────────────────────────────────────────────────────

        public async Task<(bool Success, string Message, BatchUploadRecord? Record)> Save(
            int userId, string filename, string channel, int rowCount)
        {
            var body = new
            {
                filename,
                channel,
                status   = "Processed",
                rowCount
            };
            var response = await _service.Client.PostAsJsonAsync(
                $"api/BatchPaymentUpload/Save/{userId}", body);

            if (response.IsSuccessStatusCode)
            {
                var record = await response.Content.ReadFromJsonAsync<BatchUploadRecord>(_opts);
                return (true, string.Empty, record);
            }

            var data = await response.Content.ReadAsStringAsync();
            try
            {
                using var json = JsonDocument.Parse(data);
                if (json.RootElement.TryGetProperty("message", out var msg))
                    return (false, msg.GetString() ?? "Save failed.", null);
            }
            catch (JsonException) { }
            return (false, $"Save failed ({(int)response.StatusCode}).", null);
        }
    }
}
