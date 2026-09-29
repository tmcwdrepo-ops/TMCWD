using System;

namespace TMCWD.Model.Billing
{
    public class DebitCreditMemo
    {
        public int Id { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string DcmType { get; set; } = string.Empty;
        public string MemoType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime DcmDate { get; set; }
        public DateTime? BillMonthBasis { get; set; }
        public string Explanation { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public int CreatedBy { get; set; }
        public DateTime DateCreated { get; set; }
        public int UpdatedBy { get; set; }
        public DateTime DateUpdated { get; set; }
    }
}
