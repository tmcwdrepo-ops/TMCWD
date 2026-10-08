using System.Globalization;

namespace TMCWD.Model.Billing;

public static class OtherChargeInstallments
{
    public static int ParseCount(string? value)
    {
        if (string.IsNullOrEmpty(value) || !value.All(c => c is >= '0' and <= '9')
            || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 1)
            throw new InvalidOperationException("Number of Installments must be a positive whole number.");
        return count;
    }

    public static decimal BaseAmount(decimal amount, int count)
    {
        if (amount <= 0m || amount > 9999.99m || decimal.Round(amount, 2) != amount)
            throw new InvalidOperationException("Amount must be between 0.01 and 9999.99 with at most two decimal places.");
        if (count < 1 || count > amount * 100m)
            throw new InvalidOperationException("Each installment must be at least 0.01.");
        return decimal.Floor(amount / count * 100m) / 100m;
    }
}

public sealed class CollectibleOtherCharge
{
    public int Id { get; set; }
    public long InstallmentId { get; set; }
    public int InstallmentNumber { get; set; }
    public DateTime DueBillingPeriod { get; set; }
    public int Type { get; set; }
    public string BillingReferenceId { get; set; } = string.Empty;
    public string? PayableIn { get; set; } = string.Empty;
    public string Particulars { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public sealed class OtherChargeInstallmentRecord
{
    public string? ActivatedBillingReferenceId { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public long Id { get; set; }
    public int InstallmentNumber { get; set; }
    public DateTime DueBillingPeriod { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public long? SettledPaymentId { get; set; }
    public string? PaymentReference { get; set; }
}
