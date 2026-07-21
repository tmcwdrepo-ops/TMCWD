using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using TMCWD.Model.Administrator;
using TMCWD.Model.Billing.Interfaces;
using TMCWD.Services;

namespace TMCWD.Billing
{
    public class GCashWallet : EWalletBase
    {

        #region fields

        private WebService service = new();

        #endregion

        #region constructors

        public GCashWallet() : base() { }

        #endregion

        #region methods

        public override async Task<bool> SendPayment(User user, PaymentBase payment)
        {
            bool isSuccess = false;
            
            EndpointTransaction epTrans = new(service);
            var endpoint = await epTrans.GetByTypeAndName(GatewayType.GCash, "send");

            var url = $"{endpoint.EndpointUrl}/send";

            var content = JsonContent.Create(payment);
             
            var response = await service.Client.PostAsync(url, content);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new Exception(data);

            this.Data = data;

            EWalletLogTransaction logTransaction = new(service);
            var eWalletLog = logTransaction.SaveUpdate(user.Id, this);

            return isSuccess;
        }

        #endregion



    }
}
