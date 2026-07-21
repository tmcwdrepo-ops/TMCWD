using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.CustomerSupport.Interfaces
{
    public interface IRequest
    {

        public int Id { get; set; }

        public string ControlNumber { get; set; }

        public int CustomerId { get; set; }

        public int AccountId { get; set; }
        public RequestStatus Status { get; set; }

        public int CreatedBy { get; set; }

        public DateTime DateCreated { get; set; }

        public int UpdatedBy { get; set; }

        public DateTime DateUpdated { get; set; }

    }
}
