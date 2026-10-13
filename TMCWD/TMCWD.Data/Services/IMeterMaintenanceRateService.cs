using TMCWD.Data.Entities;

namespace TMCWD.Data.Services;

public interface IMeterMaintenanceRateService
{
    Task<MeterMaintenanceRate> GetForBillingDate(DateOnly billingDate);
    Task<List<MeterMaintenanceRate>> GetAll();
    Task<MeterMaintenanceRate> SaveUpdate(long userId, MeterMaintenanceRate rate);
}
