using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using TMCWD.Model.Billing;
using TMCWD.Services;

namespace TMCWD.Billing;

public class MeterMaintenanceRateTransaction(WebService service)
{
    public async Task<MeterMaintenanceRateDto?> GetForBillingDate(DateOnly billingDate)
    {
        var path = $"api/MeterMaintenanceRate/by-billing-date/{billingDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        using var response = await service.Client.GetAsync(path);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new InvalidOperationException(await ReadMessage(response));
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Unable to load the Meter Maintenance configuration.", null, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<MeterMaintenanceRateDto>()
            ?? throw new InvalidOperationException("The Meter Maintenance configuration response was empty.");
    }

    public async Task<List<MeterMaintenanceRateDto>> GetAll()
    {
        using var response = await service.Client.GetAsync("api/MeterMaintenanceRate");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<MeterMaintenanceRateDto>>() ?? [];
    }

    public async Task<MeterMaintenanceRateDto> SaveUpdate(MeterMaintenanceRateRequest request)
    {
        using var response = await service.Client.PostAsJsonAsync("api/MeterMaintenanceRate", request);
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new InvalidOperationException(await ReadMessage(response));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MeterMaintenanceRateDto>()
            ?? throw new InvalidOperationException("The Meter Maintenance configuration response was empty.");
    }

    private static async Task<string> ReadMessage(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(content);
            if (document.RootElement.TryGetProperty("message", out var message)
                && message.ValueKind == System.Text.Json.JsonValueKind.String)
                return message.GetString() ?? "The Meter Maintenance configuration is invalid.";
        }
        catch (System.Text.Json.JsonException)
        {
        }
        return "The Meter Maintenance configuration is invalid.";
    }
}
