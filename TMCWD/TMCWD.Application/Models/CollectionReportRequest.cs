namespace TMCWD.Application.Models
{
    /// <summary>
    /// Request body for <c>POST /Collections/CollectionReportPdf</c> and
    /// <c>POST /Collections/CollectionReportExcel</c>.
    /// </summary>
    public class CollectionReportRequest
    {
        /// <summary>Inclusive start date (date part only).</summary>
        public DateTime FromDate { get; set; }

        /// <summary>Inclusive end date (date part only).</summary>
        public DateTime ToDate { get; set; }

        /// <summary>0 = all collectors; positive int = filter by cashier user ID.</summary>
        public int CollectorId { get; set; }

        /// <summary>"OR" (sort by OR number) or "accountNo" (sort by account number).</summary>
        public string SortBy { get; set; } = "OR";
    }

    /// <summary>Flat row produced by <c>BuildReportRows</c>.</summary>
    public class CollectionReportRow
    {
        public string   OrNumber      { get; set; } = string.Empty;
        public string   AccountNumber { get; set; } = string.Empty;
        public string   AccountHolder { get; set; } = string.Empty;
        public decimal  Amount        { get; set; }
        public string   Method        { get; set; } = string.Empty;
        public string   CollectorName { get; set; } = string.Empty;
        public DateTime DateCollected { get; set; }
    }
}
