using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.Billing.Interfaces
{
    public interface IReadingSheetTemplate
    {

        public int Id { get; set; }

        public int Name { get; set; }

        public int UserId { get; set; }

        public int ZoneBookId { get; set; }

        public int CreatedBy { get; set; }

        public DateTime DateCreated { get; set; }

        public int UpdatedBy { get; set; }

        public DateTime DateUpdated { get; set; }

    }
}
