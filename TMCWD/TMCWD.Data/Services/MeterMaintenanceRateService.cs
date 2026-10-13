using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Services;

public class MeterMaintenanceRateService(UserDbContext db) : IMeterMaintenanceRateService
{
    private const string ConfigurationLockName = "tmcwd:meter_maintenance_rates";

    public async Task<MeterMaintenanceRate> GetForBillingDate(DateOnly billingDate)
    {
        var matches = await db.MeterMaintenanceRates.AsNoTracking()
            .Where(rate => rate.EffectiveFrom <= billingDate
                && (rate.EffectiveTo == null || rate.EffectiveTo >= billingDate))
            .ToListAsync();

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new KeyNotFoundException(
                $"No Meter Maintenance rate is configured for billing date {billingDate:yyyy-MM-dd}. Posting is blocked; refer the date for Finance review."),
            _ => throw new InvalidOperationException(
                $"Multiple Meter Maintenance rates match billing date {billingDate:yyyy-MM-dd}. Resolve the overlapping configuration before posting.")
        };
    }

    public Task<List<MeterMaintenanceRate>> GetAll() =>
        db.MeterMaintenanceRates.AsNoTracking()
            .OrderBy(rate => rate.EffectiveFrom)
            .ToListAsync();

    public async Task<MeterMaintenanceRate> SaveUpdate(long userId, MeterMaintenanceRate rate)
    {
        if (!await db.Users.AnyAsync(user => user.Id == userId
            && user.Role == (int)UserRole.SuperAdmin
            && user.IsActive == true
            && user.IsVerified == true))
            throw new UnauthorizedAccessException();

        Validate(rate);

        await using var transaction = await db.Database.BeginTransactionAsync();
        var lockAcquired = false;
        try
        {
            lockAcquired = await AcquireConfigurationLock(
                db.Database.GetDbConnection(),
                db.Database.CurrentTransaction!.GetDbTransaction());
            if (!lockAcquired)
                throw new InvalidOperationException("Could not acquire the Meter Maintenance configuration lock.");

            var existingRates = await db.MeterMaintenanceRates
                .AsNoTracking()
                .OrderBy(existing => existing.EffectiveFrom)
                .ToListAsync();
            var existing = existingRates.SingleOrDefault(candidate => candidate.Id == rate.Id);
            if (rate.Id > 0 && existing == null)
                throw new KeyNotFoundException("The Meter Maintenance rate was not found.");

            if (existingRates.Any(candidate => candidate.Id != rate.Id
                && IntervalsOverlap(candidate.EffectiveFrom, candidate.EffectiveTo, rate.EffectiveFrom, rate.EffectiveTo)))
                throw new InvalidOperationException("The Meter Maintenance effective period overlaps an existing rate.");

            var updatedRates = existingRates.Where(candidate => candidate.Id != rate.Id).ToList();
            updatedRates.Add(rate);
            if (updatedRates.SelectMany((candidate, index) => updatedRates.Skip(index + 1)
                    .Select(other => (candidate, other)))
                .Any(pair => IntervalsOverlap(pair.candidate.EffectiveFrom, pair.candidate.EffectiveTo,
                    pair.other.EffectiveFrom, pair.other.EffectiveTo)))
                throw new InvalidOperationException("The Meter Maintenance schedule already contains overlapping effective periods.");
            await EnsurePostedBillingDatesKeepSameRate(existingRates, updatedRates);

            var now = DateTime.Now;
            if (existing == null)
            {
                rate.CreatedBy = userId;
                rate.DateCreated = now;
                rate.UpdatedBy = userId;
                rate.DateUpdated = now;
                db.MeterMaintenanceRates.Add(rate);
            }
            else
            {
                rate.CreatedBy = existing.CreatedBy;
                rate.DateCreated = existing.DateCreated;
                rate.UpdatedBy = userId;
                rate.DateUpdated = now;
                db.MeterMaintenanceRates.Update(rate);
            }

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return rate;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            if (lockAcquired)
                await ReleaseConfigurationLock(db.Database.GetDbConnection());
        }
    }

    private static void Validate(MeterMaintenanceRate rate)
    {
        if (rate.Amount < 0m || rate.EffectiveTo.HasValue && rate.EffectiveTo.Value < rate.EffectiveFrom)
            throw new ArgumentException("The Meter Maintenance amount or effective date interval is invalid.");
    }

    private static bool IntervalsOverlap(DateOnly startA, DateOnly? endA, DateOnly startB, DateOnly? endB) =>
        startA <= (endB ?? DateOnly.MaxValue)
        && startB <= (endA ?? DateOnly.MaxValue);

    private async Task EnsurePostedBillingDatesKeepSameRate(
        IReadOnlyCollection<MeterMaintenanceRate> before,
        IReadOnlyCollection<MeterMaintenanceRate> after)
    {
        var postedDates = await db.Billings.AsNoTracking()
            .Select(billing => billing.BillingPeriod)
            .Distinct()
            .ToListAsync();

        foreach (var postedDate in postedDates)
        {
            var date = DateOnly.FromDateTime(postedDate);
            var beforeMatches = before.Where(rate => rate.EffectiveFrom <= date
                && (rate.EffectiveTo == null || rate.EffectiveTo >= date)).ToList();
            var afterMatches = after.Where(rate => rate.EffectiveFrom <= date
                && (rate.EffectiveTo == null || rate.EffectiveTo >= date)).ToList();
            if (beforeMatches.Count != afterMatches.Count
                || !beforeMatches.Select(rate => rate.Amount).OrderBy(amount => amount)
                    .SequenceEqual(afterMatches.Select(rate => rate.Amount).OrderBy(amount => amount)))
                throw new InvalidOperationException(
                    $"The configuration change would alter rate selection for an existing billing period ({date:yyyy-MM-dd}).");
        }
    }

    private static async Task<bool> AcquireConfigurationLock(DbConnection connection, DbTransaction transaction)
    {
        await using var command = CreateLockCommand(connection, transaction, "SELECT GET_LOCK(@lockName, 10)");
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture) == 1;
    }

    private static async Task ReleaseConfigurationLock(DbConnection connection)
    {
        await using var command = CreateLockCommand(connection, null, "SELECT RELEASE_LOCK(@lockName)");
        await command.ExecuteScalarAsync();
    }

    private static DbCommand CreateLockCommand(DbConnection connection, DbTransaction? transaction, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@lockName";
        parameter.Value = ConfigurationLockName;
        command.Parameters.Add(parameter);
        return command;
    }
}
