namespace TMCWD.Data.Services;

public sealed class BillingAdjustmentConflictException(string message) : InvalidOperationException(message);
