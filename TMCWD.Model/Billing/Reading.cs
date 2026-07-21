using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using TMCWD.Model.Billing.Interfaces;

namespace TMCWD.Model.Billing
{
    public class Reading : IReading
    {

        #region constructors

        public Reading() { }

        #endregion

        #region members

        [DisplayName("Id")]
        public int Id { get; set; }

        [DisplayName("Account Id")]
        public int AccountId { get; set; }

        [DisplayName("Zone Book Id")]
        public int ZoneBookId { get; set; }

        [DisplayName("Render Id")]
        public int ReaderId { get; set; }

        [DisplayName("Current Reading")]
        public decimal CurrentReading { get; set; }

        [DisplayName("Billing Period")]
        public DateTime BillingPeriod { get; set; }

        [DisplayName("Created By")]
        public int CreatedBy { get; set; }

        [DisplayName("Date Created")]
        public DateTime DateCreated { get; set; }

        [DisplayName("Updated By")]
        public int UpdatedBy { get; set; }

        [DisplayName("Date Updated")]
        public DateTime DateUpdated { get; set; }

        #endregion

    }
}
