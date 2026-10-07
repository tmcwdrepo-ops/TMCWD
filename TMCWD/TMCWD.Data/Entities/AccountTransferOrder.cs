using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities;

[Table("account_transfer_orders")]
public class AccountTransferOrder
{
    [Key, Column("id")]
    public long Id { get; set; }
    [Column("account_id")]
    public long AccountId { get; set; }
    [Column("job_order_number")]
    public string JobOrderNumber { get; set; } = string.Empty;
    [Column("job_order_date")]
    public DateTime JobOrderDate { get; set; }
    [Column("old_account_number")]
    public string OldAccountNumber { get; set; } = string.Empty;
    [Column("new_account_number")]
    public string NewAccountNumber { get; set; } = string.Empty;
    [Column("zone")]
    public int Zone { get; set; }
    [Column("book")]
    public int Book { get; set; }
    [Column("transfer_date")]
    public DateTime TransferDate { get; set; }
    [Column("status")]
    public string Status { get; set; } = string.Empty;
    [Column("processed_by")]
    public long ProcessedBy { get; set; }
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
