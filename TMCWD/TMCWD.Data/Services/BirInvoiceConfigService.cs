using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services;

public class BirInvoiceConfigService(UserDbContext db) : IBirInvoiceConfigService
{
    public static void Validate(BirInvoiceConfig config)
    {
        var digits = config.SeriesTo.ToString(System.Globalization.CultureInfo.InvariantCulture).Length;
        var width = config.SeriesWidth ?? digits;
        if (string.IsNullOrWhiteSpace(config.PTUNumber) || config.PTUNumber.Length > 100
            || config.SeriesFrom < 1 || config.SeriesTo < config.SeriesFrom || width < digits || width > 19
            || (config.InvoicePrefix?.Length ?? 0) > 10 || width + (config.InvoicePrefix?.Length ?? 0) > 30
            || config.EffectiveFrom > config.EffectiveTo)
            throw new ArgumentException("The BIR invoice series, width, prefix or effective dates are invalid.");
    }
    public async Task<BirInvoiceConfig?> GetActive() =>
        await db.BirInvoiceConfigs
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.PermitDateIssued)
            .FirstOrDefaultAsync();

    public async Task<List<BirInvoiceConfig>> GetAll() =>
        await db.BirInvoiceConfigs
            .OrderByDescending(c => c.PermitDateIssued)
            .ToListAsync();

    public async Task<BirInvoiceConfig> SaveUpdate(int userId, BirInvoiceConfig config)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId && u.Role == (int)UserRole.SuperAdmin
            && u.IsActive == true && u.IsVerified == true)) throw new UnauthorizedAccessException();
        Validate(config);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var existing = await db.BirInvoiceConfigs.FromSqlRaw("SELECT * FROM bir_invoice_configs ORDER BY Id FOR UPDATE")
            .AsNoTracking().ToListAsync();
        var saved = existing.SingleOrDefault(c => c.Id == config.Id);
        if (config.Id > 0 && saved == null) throw new KeyNotFoundException();
        if (saved != null && await db.ServiceInvoiceSeq.AnyAsync(s => s.Id == config.Id)
            && (saved.SeriesFrom != config.SeriesFrom || saved.SeriesTo != config.SeriesTo
                || saved.InvoicePrefix != config.InvoicePrefix || saved.SeriesWidth != config.SeriesWidth))
            throw new InvalidOperationException("A used invoice series cannot be changed. Create a new BIR configuration.");
        var now = DateTime.Now;

        if (config.IsActive)
        {
            // Deactivate all other configs first so exactly one is active.
            var others = await db.BirInvoiceConfigs
                .Where(c => c.IsActive && c.Id != config.Id)
                .ToListAsync();
            foreach (var o in others)
            {
                o.IsActive = false;
                o.UpdatedBy = userId;
                o.DateUpdated = now;
            }
        }

        if (config.Id > 0)
        {
            config.CreatedBy = saved!.CreatedBy;
            config.DateCreated = saved.DateCreated;
            config.UpdatedBy = userId;
            config.DateUpdated = now;
            var tracked = db.BirInvoiceConfigs.Local.SingleOrDefault(c => c.Id == config.Id);
            if (tracked != null && !ReferenceEquals(tracked, config))
            {
                db.Entry(tracked).CurrentValues.SetValues(config);
                config = tracked;
            }
            db.BirInvoiceConfigs.Update(config);
        }
        else
        {
            config.CreatedBy = userId;
            config.DateCreated = now;
            config.UpdatedBy = userId;
            config.DateUpdated = now;
            db.BirInvoiceConfigs.Add(config);
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return config;
    }
}
