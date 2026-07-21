using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.Billing.Interfaces
{
    public interface IChargeType
    {

        public int Id { get; set; }

        public string Name { get; set; }

        public FeeClassification ClassificationId { get; set; }

        public int CreatedBy { get; set; }

        public DateTime DateCreated { get; set; }

        public int UpdatedBy { get; set; }

        public DateTime DateUpdated { get; set; }

    }
}
