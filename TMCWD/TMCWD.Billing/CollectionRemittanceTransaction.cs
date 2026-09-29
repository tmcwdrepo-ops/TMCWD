using System.Net.Http.Json;
using TMCWD.Model.Billing.Requests;
using TMCWD.Services;

namespace TMCWD.Billing;

public sealed class CollectionRemittanceTransaction
{
    private readonly WebService _service;

    public CollectionRemittanceTransaction(WebService service)
    {
        _service = service;
    }

    public async Task<(bool Success, string Message)> Save(CollectionRemittanceRequest request)
    {
        try
        {
            var response = await _service.Client.PostAsJsonAsync("api/CollectionRemittance/Save", request);
            var message = await response.Content.ReadAsStringAsync();
            return response.IsSuccessStatusCode
                ? (true, string.Empty)
                : (false, string.IsNullOrWhiteSpace(message) ? response.ReasonPhrase ?? "Save failed." : message);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
