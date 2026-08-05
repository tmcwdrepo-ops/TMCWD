using System;
using System.ComponentModel;
using TMCWD.Model.Billing.Interfaces;

namespace TMCWD.Model.Billing
{
    public class Reading : IReading
    {
        public Reading() { }

        [DisplayName("Id")]
        public int Id { get; set; }

        [DisplayName("Account Id")]
        public int AccountId { get; set; }

        [DisplayName("Reading Sheet Id")]
        public int ReadingSheetId { get; set; }

        [DisplayName("Current Reading")]
        public decimal CurrentReading { get; set; }

        [DisplayName("Status")]
        public int Status { get; set; }

        [DisplayName("Created By")]
        public int CreatedBy { get; set; }

        [DisplayName("Date Created")]
        public DateTime DateCreated { get; set; }

        [DisplayName("Updated By")]
        public int? UpdatedBy { get; set; }

        [DisplayName("Date Updated")]
        public DateTime? DateUpdated { get; set; }
    }
}