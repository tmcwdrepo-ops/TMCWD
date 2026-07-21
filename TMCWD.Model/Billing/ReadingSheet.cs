using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
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
        public DateTime BillingDate { get; set; }

        [DisplayName("Zone Book Id")]
        public int ZoneBookId { get; set; }

        [DisplayName("Assigned To")]
        public int AssignedTo { get; set; }

        [DisplayName("Created By")]
        public int CreatedBy { get; set; }

        [DisplayName("Date Created")]
        public DateTime DateCreated { get; set; }

        [DisplayName("Updated By")]
        public int UpdatedBy { get; set; }

        [DisplayName("Date Updated")]
        public DateTime DateUpdated { get; set; }

    }
}
