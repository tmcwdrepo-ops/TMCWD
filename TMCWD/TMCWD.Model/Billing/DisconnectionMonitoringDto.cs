namespace TMCWD.Model.Billing;

public sealed class DisconnectionMonitoringDto
{
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string ServiceAddress { get; set; } = string.Empty;
    public string MeterNumber { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public int Zone { get; set; }
    public int Book { get; set; }
    public string Status { get; set; } = "CURRENT";
    public string PhysicalServiceStatus { get; set; } = "NO CONFIRMED EVENT";
    public string OperationalAccountStatus { get; set; } = "Unknown";
    public string? FirstBillingReference { get; set; }
    public DateTime? FirstBillingDate { get; set; }
    public decimal? FirstBillingRemainingAmount { get; set; }
    public string? SecondBillingReference { get; set; }
    public DateTime? SecondBillingDate { get; set; }
    public decimal? SecondBillingRemainingAmount { get; set; }
    public DateTime? SubjectDate { get; set; }
    public DateTime? ScheduledDisconnectionDate { get; set; }
    public int? DaysRemaining { get; set; }
    public decimal OutstandingWaterBalance { get; set; }
    public decimal ActivePenaltyBalance { get; set; }
    public decimal CollectibleOtherCharges { get; set; }
    public decimal ReconnectionFeeBalance { get; set; }
    public decimal TotalCollectibleBalance { get; set; }
    public string? EventId { get; set; }
    public DateTime? ActualDisconnectionDateUtc { get; set; }
    public DateTime? ActualReconnectionDateUtc { get; set; }
    public bool FinancialReviewRequired { get; set; }
}

// No client-controlled actor, dates, fee, balance or arbitrary status.
public sealed record ServiceConfirmationRequest(Guid OperationId);
