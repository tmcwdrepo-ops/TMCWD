namespace TMCWD.Model.Billing;

/// <summary>
/// Read-only audit record for a single billing_adjustments row.
/// Resolved user names are informational; CreatedBy/UpdatedBy IDs are
/// the authoritative persisted audit identity.
/// Reason is null for historical rows that pre-date Phase 6I-C1.
/// ServiceInvoiceNumber is null for billings that pre-date Phase 6D.
/// </summary>
public sealed class BillingAdjustmentHistoryDto
{
    public long   Id                   { get; init; }
    public string BillingReferenceId   { get; init; } = string.Empty;
    public string? ServiceInvoiceNumber { get; init; }
    public string AdjustmentType       { get; init; } = string.Empty;
    public decimal Amount              { get; init; }
    public string? Reason              { get; init; }
    public long   CreatedBy            { get; init; }
    public string? CreatedByName       { get; init; }
    public DateTime DateCreated        { get; init; }
    public long   UpdatedBy            { get; init; }
    public string? UpdatedByName       { get; init; }
    public DateTime DateUpdated        { get; init; }
}
