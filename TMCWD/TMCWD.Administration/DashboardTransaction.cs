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
}
