using TMCWD.Model.Billing;
using TMCWD.Model.CustomerSupport;

namespace TMCWD.Billing
{
    /// <summary>
    /// Shared water charge computation — the single source of truth for
    /// tiered usage billing. Used by BillingController (live reading entry)
    /// and ReadingSheetController (reading sheet account list) so both
    /// surfaces always agree on the amount for a given usage.
    /// </summary>
    public static class WaterChargeCalculator
    {
        public const decimal WaterMeterMaintenanceFee = 20.00m;

        public static async Task<decimal> ComputeWaterCharge(
            WaterRateTransaction waterRateTrans,
            int previousReading,
            int presentReading,
            AccountClassification classification,
            decimal meterSize)
        {
            if (presentReading < previousReading)
                return -1;

            var rate = await waterRateTrans.GetByClassificationAndMeterSize(
                classification,
                meterSize);

            if (rate == null)
            {
                throw new InvalidOperationException(
                    $"No active water rate found for " +
                    $"classification '{classification}' " +
                    $"and meter size {meterSize}.");
            }

            int usage = presentReading - previousReading;

            decimal total;

            if (usage <= 10)
            {
                total = rate.MinimumCharge;
            }
            else if (usage <= 20)
            {
                total = rate.MinimumCharge
                      + ((usage - 10) * rate.Rate11To20);
            }
            else if (usage <= 30)
            {
                total = rate.MinimumCharge
                      + (10 * rate.Rate11To20)
                      + ((usage - 20) * rate.Rate21To30);
            }
            else if (usage <= 40)
            {
                total = rate.MinimumCharge
                      + (10 * rate.Rate11To20)
                      + (10 * rate.Rate21To30)
                      + ((usage - 30) * rate.Rate31To40);
            }
            else
            {
                total = rate.MinimumCharge
                      + (10 * rate.Rate11To20)
                      + (10 * rate.Rate21To30)
                      + (10 * rate.Rate31To40)
                      + ((usage - 40) * rate.Rate41Up);
            }

            total += WaterMeterMaintenanceFee;

            return total;
        }
    }
}