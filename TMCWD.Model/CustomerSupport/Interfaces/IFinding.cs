using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace TMCWD.Model.CustomerSupport.Interfaces
{
    public interface IFinding
    {
        public int Id { get; set; }

        public int JobOrderId { get; set; }

        public string Detail { get; set; }

        public int CreatedBy { get; set; }

        public DateTime DateCreated { get; set; }

        public int UpdatedBy { get; set; }

        public DateTime DateUpdated { get; set; }
    }
}
