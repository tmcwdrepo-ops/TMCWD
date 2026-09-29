using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities
{
    [Table("debit_credit_memos")]
    public class DebitCreditMemo
    {
        [Key]
        public long Id { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string DcmType { get; set; } = string.Empty;
        public string MemoType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime DcmDate { get; set; }
        public DateTime? BillMonthBasis { get; set; }
        public string Explanation { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public long CreatedBy { get; set; }
        public DateTime DateCreated { get; set; }
        public long UpdatedBy { get; set; }
        public DateTime DateUpdated { get; set; }
    }
}
