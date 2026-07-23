using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace TMCWD.Data.Entities
{
    [Table("reading_sheets")]
    public class ReadingSheet
    {

        [Key, Column("Id")]
        public System.Int64 Id { get; set; }

        [Column("Name")]
        public string Name { get; set; } = string.Empty;

        [Column("BillingDate")]
        public DateTime BillingDate { get; set; }

        [Column("DueDate")]
        public DateTime DueDate { get; set; }

        [Column("DisconnectionDate")]
        public DateTime DisconnectionDate { get; set; }

        [Column("BillingPeriodStart")]
        public DateTime BillingPeriodStart { get; set; }

        [Required, Column("ZoneBookId")]
        public int ZoneBookId { get; set; }

        [Required, Column("AssignedTo")]
        public System.Int64 AssignedTo { get; set; }

        [Column("IsCompleted")]
        public bool IsCompleted { get; set; }

        [Required, Column("CreatedBy")]
        public System.Int64 CreatedBy { get; set; }

        [Column("DateCreated")]
        public DateTime DateCreated { get; set; }

        [Column("UpdatedBy")]
        public System.Int64 UpdatedBy { get; set; }

        [Column("DateUpdated")]
        public DateTime DateUpdated { get; set; }

    }
}
