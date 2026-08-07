using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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

        [DisplayName("Reading Sheet Id")]
        public int ReadingSheetId { get; set; }

        [DisplayName("Current Reading")]
        public decimal CurrentReading { get; set; }

        [DisplayName("Status")]
        public ReadingStatus Status { get; set; } = new ReadingStatus();

        [DisplayName("Completed")]
        public bool IsCompleted { get; set; }

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
