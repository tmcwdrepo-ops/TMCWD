using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.Billing.Interfaces
{
    public interface IReading
    {

        public int Id { get; set; }

        public int AccountId { get; set; }

        public int ZoneBookId { get; set; }

        public decimal CurrentReading { get; set; }

        public int ReaderId { get; set; }

        public DateTime BillingPeriod { get; set; }

        public int CreatedBy { get; set; }

        public DateTime DateCreated { get; set; }

        public int UpdatedBy { get; set; }

        public DateTime DateUpdated { get; set; }

    }
}
