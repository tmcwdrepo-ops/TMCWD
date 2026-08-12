    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    namespace TMCWD.Data.Entities
    {
        [Table("reading_sheets")]
        public class ReadingSheet
        {
            [Key, Column("Id")]
            public long Id { get; set; }

            [Column("Name")]
            public string Name { get; set; } = string.Empty;

            [Column("DisconnectionDate")]
            public DateTime? DisconnectionDate { get; set; }

            [Column("BillingPeriodStart")]
            public DateTime? BillingPeriodStart { get; set; }

            [Column("BillingDate")]
            public DateTime? BillingDate { get; set; }

            [Column("DueDate")]
            public DateTime? DueDate { get; set; }

            [Required, Column("ZoneBookId")]
            public int ZoneBookId { get; set; }

            [Required, Column("AssignedTo")]
            public System.Int64 AssignedTo { get; set; }

            [Required, Column("CreatedBy")]
            public System.Int64 CreatedBy { get; set; }

            [Column("SeqFrom")]
            public int? SeqFrom { get; set; }

            [Column("SeqTo")]
            public int? SeqTo { get; set; }

            [Column("Status")]
            public ReadingStatus Status { get; set; } = ReadingStatus.Created;

            [Column("DateCreated")]
            public DateTime? DateCreated { get; set; }

            [Column("DateUpload")]
            public DateTime? DateUpload { get; set; }
        }
    }