namespace TMCWD.Model.Billing.Responses;

public sealed record MobileReadingSheetDownloadDto(
    MobileDownloadSheetDto ReadingSheet, List<MobileDownloadAccountDto> Accounts);

public sealed record MobileDownloadSheetDto(long Id, string? Name, DateTime? BillingDate,
    DateTime? DueDate, DateTime? DisconnectionDate, DateTime? BillingPeriodStart,
    int ZoneBookId, int? SeqFrom, int? SeqTo, long AssignedTo, ReadingStatus Status,
    DateTime? DateCreated, DateTime? DateUpload);

public sealed record MobileDownloadAccountDto(long AccountId, string AccountNumber,
    string Name, string MeterNumber, string Address, int ZoneBookId, int Zone, int Book,
    int Sequence, int Classification, decimal MeterSize, decimal PreviousReading,
    DateTime? PreviousReadingDate, long? ReadingId, decimal? CurrentReading,
    DateTime? ReadingDate, ReadingStatus? ReadingStatus, string Remarks = "", bool InRoster = true);
