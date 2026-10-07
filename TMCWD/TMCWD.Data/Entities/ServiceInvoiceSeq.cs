using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TMCWD.Data.Entities;

/// <summary>
/// One counter per BIR configuration. The key is the configuration ID.
/// All writes must be inside a transaction that holds a FOR UPDATE lock.
/// </summary>
[Table("service_invoice_seq")]
public class ServiceInvoiceSeq
{
    [Key, Column("Id")]
    public long Id { get; set; }

    [Column("LastValue")]
    public long LastValue { get; set; }
}
