using System;
using System.ComponentModel;
using TMCWD.Model.Billing.Interfaces;

namespace TMCWD.Model.Billing
{
    public class ReadingSheet : IReadingSheet
    {
        public ReadingSheet() { }

        [DisplayName("Id")]
        public int Id { get; set; }

        [DisplayName("Name")]
        public string Name { get; set; } = string.Empty;

        [DisplayName("Billing Date")]
        public DateTime? BillingDate { get; set; }

        [DisplayName("Due Date")]
        public DateTime? DueDate { get; set; }

        [DisplayName("Disconnection Date")]
        public DateTime? DisconnectionDate { get; set; }

        [DisplayName("Billing Period Start")]
        public DateTime? BillingPeriodStart { get; set; }

        [DisplayName("Zone Book Id")]
        public int ZoneBookId { get; set; }

        [DisplayName("Seq From")]
        public int? SeqFrom { get; set; }

        [DisplayName("Seq To")]
        public int? SeqTo { get; set; }

        [DisplayName("Assigned To")]
        public int AssignedTo { get; set; }

        [DisplayName("Status")]
        public ReadingStatus Status { get; set; } = ReadingStatus.Created;

        [DisplayName("Created By")]
        public int CreatedBy { get; set; }

        [DisplayName("Date Created")]
        public DateTime? DateCreated { get; set; }
        public object DateUpload { get; set; }
    }
}