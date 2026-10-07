using TMCWD.Data.Entities;

namespace TMCWD.Data.Services;

public interface IBirInvoiceConfigService
{
    /// <summary>
    /// Returns the currently active BIR invoice configuration,
    /// or null if none has been set up yet.
    /// </summary>
    Task<BirInvoiceConfig?> GetActive();

    /// <summary>
    /// Returns all configurations (active and inactive), ordered by
    /// PermitDateIssued descending. Used for administration.
    /// </summary>
    Task<List<BirInvoiceConfig>> GetAll();

    /// <summary>
    /// Saves a new or updated configuration.  When IsActive is true, all
    /// other configurations are automatically deactivated so exactly one
    /// active config exists at a time.
    /// </summary>
    Task<BirInvoiceConfig> SaveUpdate(int userId, BirInvoiceConfig config);
}
