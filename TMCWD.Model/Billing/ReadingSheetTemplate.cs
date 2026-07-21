using System;
using System.Collections.Generic;
using System.Text;
using TMCWD.Model.Billing.Interfaces;

namespace TMCWD.Model.Billing
{
    public class ReadingSheetTemplate : IReadingSheetTemplate
    {

        #region constructors

        public ReadingSheetTemplate() { }

        #endregion

        #region properties

        public int Id { get; set; }
        public int Name { get; set; }
        public int UserId { get; set; }
        public int ZoneBookId { get; set; }
        public int CreatedBy { get; set; }
        public DateTime DateCreated { get; set; }
        public int UpdatedBy { get; set; }
        public DateTime DateUpdated { get; set; }

        #endregion

    }
}
