using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.Billing.Interfaces
{
    public interface IReadingSheet
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public DateTime BillingDate { get; set; }

        public int ZoneBookId { get; set; }

        public int AssignedTo { get; set; }

        public int CreatedBy { get; set; }

        public DateTime DateCreated { get; set; }

        public int UpdatedBy { get; set; }

        public DateTime DateUpdated { get; set; }

    }
}
