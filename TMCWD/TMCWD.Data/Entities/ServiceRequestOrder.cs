using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities;

[Table("service_request_orders")]
public class ServiceRequestOrder
{
    [Key, Column("id")] public long Id { get; set; }
    [Column("is_non_customer")] public bool IsNonCustomer { get; set; }
    [Column("account_id")] public long? AccountId { get; set; }
    [Column("account_number")] public string AccountNumber { get; set; } = string.Empty;
    [Column("account_name")] public string AccountName { get; set; } = string.Empty;
    [Column("address")] public string Address { get; set; } = string.Empty;
    [Column("reported_by")] public string ReportedBy { get; set; } = string.Empty;
    [Column("contact_number")] public string ContactNumber { get; set; } = string.Empty;
    [Column("job_order_number")] public string JobOrderNumber { get; set; } = string.Empty;
    [Column("job_order_date")] public DateTime JobOrderDate { get; set; }
    [Column("source")] public string Source { get; set; } = string.Empty;
    [Column("job_order_type")] public string JobOrderType { get; set; } = string.Empty;
    [Column("job_description")] public string JobDescription { get; set; } = string.Empty;
    [Column("referred_to_user_id")] public long? ReferredToUserId { get; set; }
    [Column("date_referred")] public DateTime? DateReferred { get; set; }
    [Column("name")] public string Name { get; set; } = string.Empty;
    [Column("particulars")] public string Particulars { get; set; } = string.Empty;
    [Column("received_by_user_id")] public long ReceivedByUserId { get; set; }
    [Column("status")] public string Status { get; set; } = string.Empty;
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}
