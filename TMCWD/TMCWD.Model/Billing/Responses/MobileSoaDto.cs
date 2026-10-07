using TMCWD.Model.Billing;

namespace TMCWD.Model.Billing.Responses;

// Amounts are persisted Billing values, not a recalculated account balance.
// ServiceInvoiceNumber is null for historical billings that pre-date Phase 6D.
// QrPayload encodes only the lookup key — no personal or financial data.
// BirConfig is null when no active BIR configuration has been set up yet.
// MeterReaderName and ReadingRemarks are informational only; not financial.
// PCA, Arrears: no authoritative rule — remain nullable.
//
// Penalty semantics:
//   CurrentPenaltyAmount  — sum of persisted Penalty rows; 0 before DueDate, null if no rows.
//   PenaltyAfterDueAmount — informational 10% of BillAmount (server-derived, never recalculate in Flutter).
//   TotalAfterDue         — TotalDue + PenaltyAfterDueAmount; null when TotalDue is null.
public sealed record MobileSoaDto(
    long ReadingSheetId,
    string? SheetName,
    long ReadingId,
    long BillingId,
    string BillingReferenceId,
    long AccountId,
    string AccountNumber,
    string Name,
    string Address,
    string MeterNumber,
    int Classification,
    decimal MeterSize,
    decimal PreviousReading,
    decimal CurrentReading,
    decimal Usage,
    DateTime ReadingDate,
    DateTime BillingPeriod,
    DateTime? BillingDate,
    DateTime? DueDate,
    DateTime? DisconnectionDate,
    decimal BillingAdjustment,
    decimal OtherCharges,
    decimal TotalBillAmount,
    decimal RemainingAmount,
    PaymentStatus PaymentStatus,
    // ── Phase 6D additions ─────────────────────────────────────────────────
    string? ServiceInvoiceNumber,
    string? QrPayload,
    BirInvoiceConfigDto? BirConfig,
    string? MeterReaderName,
    string? ReadingRemarks,
    DateTime? PeriodFrom = null,
    DateTime? PeriodTo = null,
    decimal? BillAmount = null,
    decimal? MeterMaintenance = null,
    decimal? PowerCostAdjustment = null,
    decimal? ArrearsAmount = null,
    decimal? TotalDue = null,
    /// <summary>
    /// Sum of persisted Penalty rows for this billing reference.
    /// Null when there are no penalty records (before DueDate).
    /// </summary>
    decimal? CurrentPenaltyAmount = null,
    /// <summary>
    /// Informational 10% of BillAmount computed by the server using the same
    /// rounding logic as the authoritative penalty process.
    /// Never computed in Flutter. Null when TotalDue is null.
    /// </summary>
    decimal? PenaltyAfterDueAmount = null,
    /// <summary>
    /// TotalDue + PenaltyAfterDueAmount.
    /// Null when TotalDue is null.
    /// </summary>
    decimal? TotalAfterDue = null,
    IReadOnlyList<InvoiceChargeLine>? ChargeLines = null);
