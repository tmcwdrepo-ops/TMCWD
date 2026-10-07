using System.Net.Http.Json;
using TMCWD.Model.Dashboard;
using TMCWD.Services;

namespace TMCWD.Administration;

public class DashboardTransaction
{
    private readonly WebService _service;

    public DashboardTransaction(WebService service) => _service = service;

    public async Task<ServiceDashboard> GetServiceSummary()
    {
        using var response = await _service.Client.GetAsync("api/Dashboard/ServiceSummary");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ServiceDashboard>() ?? new ServiceDashboard();
    }

    public async Task<List<ServiceApplicationRecord>> GetServiceApplications(string category)
    {
        using var response = await _service.Client.GetAsync($"api/Dashboard/ServiceApplications/{Uri.EscapeDataString(category)}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ServiceApplicationRecord>>() ?? new();
    }

    public async Task<ServiceInspectionRecord> GetServiceInspection(long requestId)
    {
        using var response = await _service.Client.GetAsync($"api/Dashboard/ServiceInspection/{requestId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ServiceInspectionRecord>() ?? new ServiceInspectionRecord();
    }

    public async Task<ServiceInspectionRecord> SaveServiceInspection(long userId, ServiceInspectionRecord inspection)
    {
        using var response = await _service.Client.PostAsJsonAsync($"api/Dashboard/SaveServiceInspection/{userId}", inspection);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ServiceInspectionRecord>() ?? inspection;
    }

    public async Task<ConnectOrderRecord> GetConnectOrder(long requestId)
    {
        using var response = await _service.Client.GetAsync($"api/Dashboard/ConnectOrder/{requestId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ConnectOrderRecord>() ?? new ConnectOrderRecord();
    }

    public async Task<ConnectOrderRecord> SaveConnectOrder(long userId, ConnectOrderRecord order)
    {
        using var response = await _service.Client.PostAsJsonAsync($"api/Dashboard/SaveConnectOrder/{userId}", order);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ConnectOrderRecord>() ?? order;
    }

    public async Task<ApplicationPaymentRecord> GetApplicationPayment(long applicationId)
    {
        using var response = await _service.Client.GetAsync($"api/Dashboard/ApplicationPayment/{applicationId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApplicationPaymentRecord>() ?? new ApplicationPaymentRecord();
    }

    public async Task<ApplicationTicketRecord> GetApplicationTicket(long applicationId)
    {
        using var response = await _service.Client.GetAsync($"api/Dashboard/ApplicationTicket/{applicationId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApplicationTicketRecord>() ?? new ApplicationTicketRecord();
    }

    public async Task<NewConnectionApplicationRecord> CreateNewConnection(long userId, NewConnectionApplicationRecord application)
    {
        using var response = await _service.Client.PostAsJsonAsync($"api/Dashboard/NewConnection/{userId}", application);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NewConnectionApplicationRecord>() ?? application;
    }

    public async Task<List<DisconnectionAccountRecord>> SearchDisconnectionAccounts(string query)
    {
        using var response = await _service.Client.GetAsync($"api/Dashboard/DisconnectionAccounts?q={Uri.EscapeDataString(query)}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<DisconnectionAccountRecord>>() ?? new();
    }

    public async Task<List<DisconnectionOrderRecord>> GetDisconnectionOrders()
    {
        using var response = await _service.Client.GetAsync("api/Dashboard/DisconnectionOrders");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<DisconnectionOrderRecord>>() ?? new();
    }

    public async Task<DisconnectionOrderRecord> SaveDisconnectionOrder(long userId, DisconnectionOrderRecord order)
    {
        using var response = await _service.Client.PostAsJsonAsync($"api/Dashboard/DisconnectionOrder/{userId}", order);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DisconnectionOrderRecord>() ?? order;
    }

    public async Task<List<AccountTransferLookupRecord>> SearchAccountTransferAccounts(string query)
    {
        using var response = await _service.Client.GetAsync($"api/Dashboard/AccountTransferAccounts?q={Uri.EscapeDataString(query)}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<AccountTransferLookupRecord>>() ?? new();
    }

    public async Task<AccountTransferRecord> SaveAccountTransfer(long userId, AccountTransferRecord transfer)
    {
        using var response = await _service.Client.PostAsJsonAsync($"api/Dashboard/AccountTransfer/{userId}", transfer);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AccountTransferRecord>() ?? transfer;
    }

    public async Task<List<ServiceRequestAssigneeRecord>> GetServiceRequestAssignees()
    {
        using var response = await _service.Client.GetAsync("api/Dashboard/ServiceRequestAssignees");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ServiceRequestAssigneeRecord>>() ?? new();
    }

    public async Task<ServiceRequestOrderRecord> CreateServiceRequest(long userId, ServiceRequestOrderRecord request)
    {
        using var response = await _service.Client.PostAsJsonAsync($"api/Dashboard/ServiceRequest/{userId}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ServiceRequestOrderRecord>() ?? request;
    }

    public async Task<ServiceWorkOrderRecord> GetWorkOrder(string type, long requestId)
    {
        using var response = await _service.Client.GetAsync($"api/Dashboard/WorkOrder/{Uri.EscapeDataString(type)}/{requestId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ServiceWorkOrderRecord>() ?? new ServiceWorkOrderRecord();
    }

    public async Task<ServiceWorkOrderRecord> SaveWorkOrder(long userId, ServiceWorkOrderRecord order)
    {
        using var response = await _service.Client.PostAsJsonAsync($"api/Dashboard/SaveWorkOrder/{userId}", order);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ServiceWorkOrderRecord>() ?? order;
    }

    public async Task<HouseTappingRecord> GetHouseTapping(long requestId)
    {
        using var response = await _service.Client.GetAsync($"api/Dashboard/HouseTapping/{requestId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<HouseTappingRecord>() ?? new HouseTappingRecord();
    }

    public async Task<HouseTappingRecord> SaveHouseTapping(long userId, HouseTappingRecord record)
    {
        using var response = await _service.Client.PostAsJsonAsync($"api/Dashboard/SaveHouseTapping/{userId}", record);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<HouseTappingRecord>() ?? record;
    }

    public async Task<ActivateAccountResult> ActivateAccount(long userId, ActivateAccountRequest request)
    {
        using var response = await _service.Client.PostAsJsonAsync($"api/Dashboard/ActivateAccount/{userId}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ActivateAccountResult>() ?? new ActivateAccountResult();
    }

    public async Task<AdvanceStageResult> AdvanceStage(long userId, AdvanceStageRequest request)
    {
        using var response = await _service.Client.PostAsJsonAsync($"api/Dashboard/AdvanceStage/{userId}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AdvanceStageResult>() ?? new AdvanceStageResult();
    }

    public async Task<System.Text.Json.JsonElement> SaveServiceRequest(long userId, ServiceRequestOrderRecord request)
    {
        using var response = await _service.Client.PostAsJsonAsync($"api/Dashboard/SaveServiceRequest/{userId}", request);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException(await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
    }

    public async Task AccomplishServiceRequest(long requestId, long userId)
    {
        using var response = await _service.Client.PostAsJsonAsync($"api/Dashboard/AccomplishServiceRequest/{requestId}/{userId}", new { });
        if (!response.IsSuccessStatusCode) throw new HttpRequestException(await response.Content.ReadAsStringAsync());
    }

    public async Task<List<ServiceRequestSummaryRow>> GetServiceRequestsByDescription(string? description, string? status = null)
    {
        using var response = await _service.Client.GetAsync(
            $"api/Dashboard/ServiceRequestsByDescription?description={Uri.EscapeDataString(description ?? string.Empty)}&status={Uri.EscapeDataString(status ?? string.Empty)}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ServiceRequestSummaryRow>>() ?? new();
    }
}
