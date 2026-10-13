namespace TMCWD.Model.Billing;

public sealed record MeterMaintenanceRateDto(
    long Id,
    decimal Amount,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    long CreatedBy,
    DateTime DateCreated,
    long UpdatedBy,
    DateTime DateUpdated);

public sealed record MeterMaintenanceRateRequest(
    long Id,
    decimal Amount,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);
