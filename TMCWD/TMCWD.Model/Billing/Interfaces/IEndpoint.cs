using System;
using System.Collections.Generic;
using System.Text;

namespace TMCWD.Model.Billing.Interfaces
{
    public interface IEndpoint
    {
        public int Id { get; set; }

        public GatewayType Type { get; set; }

        public string Name { get; set; }

        public string EndpointUrl { get; set; }
    }
}
