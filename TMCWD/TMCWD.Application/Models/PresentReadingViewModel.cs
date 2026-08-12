namespace TMCWD.Application.Models
{
    /// <summary>
    /// Flat view model for a single row in the Present Reading table.
    /// </summary>
    public class PresentReadingViewModel
    {
        public int ReadingId { get; set; }
        public int AccountId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Classification { get; set; } = string.Empty;
        public string MeterNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal PresentReading { get; set; }
        public decimal PreviousReading { get; set; }
        public decimal Usage => Math.Max(0, PresentReading - PreviousReading);
        public decimal Amount { get; set; }
        public decimal MeterSize { get; set; }
    }

    /// <summary>
    /// View model returned when an account number is looked up.
    /// Populates the read-only fields in the Present Reading form.
    /// </summary>
    public class AccountLookupViewModel
    {
        public int AccountId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string MeterNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Classification { get; set; } = string.Empty;
        public decimal PreviousReading { get; set; }
        public decimal MeterSize { get; set; }
    }
}
