using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.Billing.Interfaces
{
    public interface IZoneBook
    {
        #region members

        public int Id { get; set; }

        public int Zone { get; set; }

        public int Book { get; set; }

        public string Area { get; set; }

        public int Week { get; set; }

        #endregion
    }
}
