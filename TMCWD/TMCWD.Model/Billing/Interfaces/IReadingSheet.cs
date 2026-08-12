using System;

namespace TMCWD.Model.Billing.Interfaces
{
    public interface IReadingSheet
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public DateTime? BillingDate { get; set; }

        public DateTime? DueDate { get; set; }

        public DateTime? DisconnectionDate { get; set; }

        public DateTime? BillingPeriodStart { get; set; }

        public int ZoneBookId { get; set; }

        public int? SeqFrom { get; set; }

        public int? SeqTo { get; set; }

        public int AssignedTo { get; set; }

        public ReadingStatus Status { get; set; }

        public int CreatedBy { get; set; }

        public DateTime? DateCreated { get; set; }
    }
}