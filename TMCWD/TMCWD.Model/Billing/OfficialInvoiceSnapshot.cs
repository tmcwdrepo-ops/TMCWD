namespace TMCWD.Model.Billing;

// SortOrder 0 = minimum, ascending for tiers.
public sealed record InvoiceChargeLine(string Description, decimal? Quantity, decimal UnitCost, decimal Amount, int SortOrder);

/// <summary>
/// Immutable snapshot of the water-bill components issued at PartialPost time.
/// NULL means the value was not confirmed at post time — never substitute zero.
/// Do NOT store penalty in the snapshot: penalty does not exist at post time.
/// Do NOT recompute historical snapshots: they represent the exact issued invoice.
/// schemaVersion 1: initial official invoice snapshot.
/// </summary>
public sealed record OfficialInvoiceSnapshot(
    int SchemaVersion,
    DateTime? PeriodFrom,
    DateTime? PeriodTo,
    decimal BillAmount,
    decimal MeterMaintenance,
    decimal? PowerCostAdjustment,
    decimal? ArrearsAmount,
    decimal OtherCharges,
    decimal BillingAdjustment,
    decimal TotalDue,
    IReadOnlyList<InvoiceChargeLine> ChargeLines);
