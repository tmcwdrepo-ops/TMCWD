using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using TMCWD.Model.Billing.Interfaces;

namespace TMCWD.Model.Billing
{
    public class ZoneBook : IZoneBook
    {

        #region constructors

        public ZoneBook() { }

        #endregion

        #region members

        [DisplayName("Id")]
        public int Id { get; set; }

        [DisplayName("Zone")]
        public int Zone { get; set; }

        [DisplayName("Book")]
        public int Book { get; set; }

        [DisplayName("Area")]
        public string Area { get; set; } = string.Empty;

        [DisplayName("Week")]
        public int Week { get; set; }

        #endregion

    }
}
