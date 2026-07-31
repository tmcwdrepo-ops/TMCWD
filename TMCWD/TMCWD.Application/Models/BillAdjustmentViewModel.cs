using Microsoft.AspNetCore.Mvc.Rendering;

namespace TMCWD.Application.Models
{
    public class BillAdjustmentViewModel
    {
        public string? AccountNumber { get; set; }
        public DateTime? BamDate { get; set; }
        public string? MeterReader { get; set; }
        public List<SelectListItem> MeterReaders { get; set; } = new();
        public DateTime? BillingDate { get; set; }
        public string? Remarks { get; set; }
        public List<SelectListItem> RemarksOptions { get; set; } = new();
        public string? Explanation { get; set; }
        public List<AdjustmentLineItem> AdjustmentLines { get; set; } = new();
    }

    public class AdjustmentLineItem
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public bool IsChecked { get; set; }
        public decimal? AsBilled { get; set; }
        public decimal? ShouldBe { get; set; }
        public decimal? Adjustment { get; set; }
        public bool HasAdjustmentColumn { get; set; }
    }
}
