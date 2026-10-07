namespace TMCWD.Model.Billing.Responses;

public sealed record MobileAssignedReadingSheetDto(
    long Id,
    string? Name,
    DateTime? BillingDate,
    DateTime? DueDate,
    DateTime? DisconnectionDate,
    DateTime? BillingPeriodStart,
    int ZoneBookId,
    int? SeqFrom,
    int? SeqTo,
    ReadingStatus Status);
