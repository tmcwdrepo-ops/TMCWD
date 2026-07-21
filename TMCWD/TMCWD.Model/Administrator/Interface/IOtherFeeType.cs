using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.Administrator.Interface
{
    public interface IOtherFeeType
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public decimal DefaultValue { get; set; }

        public int CreatedBy { get; set; }

        public DateTime DateCreated { get; set; }

        public int UpdatedBy { get; set; }

        public DateTime DateUpdated { get; set; }

    }
}
