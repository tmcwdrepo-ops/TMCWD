namespace TMCWD.Model.Billing;

/// <summary>
/// Read-only DTO for the active BIR invoice configuration.
/// Returned by the config endpoint and embedded in the SOA response.
/// </summary>
public sealed record BirInvoiceConfigDto(
    long Id,
    string PTUNumber,
    DateOnly PermitDateIssued,
    long SeriesFrom,
    long SeriesTo,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    string? InvoicePrefix = null,
    int? SeriesWidth = null);
