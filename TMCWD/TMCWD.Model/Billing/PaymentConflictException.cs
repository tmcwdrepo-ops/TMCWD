namespace TMCWD.Model.Billing;

public sealed class PaymentConflictException() : InvalidOperationException(
    "The bill balance changed or has already been paid. Refresh and try again.");
