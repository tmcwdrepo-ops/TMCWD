using System;
using System.Collections.Generic;
using System.Text;
using TMCWD.Model.Billing.Interfaces;

namespace TMCWD.Model.Billing
{

    public class Endpoint : IEndpoint
    {
        #region constructors

        public Endpoint() { }

        #endregion

        #region properties

        public int Id { get; set; }
        public GatewayType Type { get; set; } 
        public string Name { get; set; } = string.Empty;
        public string EndpointUrl { get; set; } = string.Empty;

        #endregion
    }

}
