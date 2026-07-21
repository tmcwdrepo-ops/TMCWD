using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TMCWD.Model.CustomerSupport;
using TMCWD.Model.Interfaces;
using TMCWD.Services;
using TMCWD.Utility.Generic;

namespace TMCWD.CustomerSupport
{
    public class AccountTransaction
    {

        #region fields

        private readonly WebService _webService;

        #endregion

        #region constructors

        public AccountTransaction(WebService webService) { _webService = webService; }

        #endregion

        #region public methods

        public Account ConvertJsonToAccount(string json)
        {
            var serializeOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<Account>(json, serializeOptions) ?? new Account();
        }

        public List<Account> ConvertJsonToAccounts(string json)
        {
            var serializeOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<Account>>(json, serializeOptions) ?? new List<Account>();
        }

        public async Task<Account> SaveUpdate(int userId, Account account)
        {
            StringBuilder sb = new();

            if (account == null) throw new Exception("Required account fields are not supplied");
            if (account.Id > 0 && String.IsNullOrEmpty(account.AccountNumber.Trim())) sb.AppendLine("Äccount number is required");
            if (account.Id > 0 && String.IsNullOrEmpty(account.MeterNumber.Trim())) sb.AppendLine("Meter number is required");
            if (account.CustomerId <= 0) sb.AppendLine("No customer has been selected for this account");
            if (String.IsNullOrEmpty(account.HouseNumber.Trim())) sb.AppendLine("Account house number is required for account creation");

            if (!String.IsNullOrEmpty(sb.ToString().Trim())) throw new Exception(sb.ToString());

            if(account.Id <= 0)
            {
                string shortGuid = Guid.NewGuid().ToString("N").Substring(0, 5);
                account.AccountNumber = $"ACCT{DateTime.Now.ToString("yyyy")}-{DateTime.Now.ToString("MM")}-{shortGuid}";
            }

            var content = JsonContent.Create(account);

            var response = await _webService.Client.PostAsync($"api/Account/SaveUpdate/{userId}", content);

            var data = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return null;

            return ConvertJsonToAccount(data);
        }

        public async Task<Account> Get(int id)
        {

            if (id <= 0) throw new Exception("Account id is not specified");

            var response = await _webService.Client.GetAsync($"api/Account/Get/{id}");

            var data = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return null;

            return ConvertJsonToAccount(data);
        }

        public async Task<Account> GetByAccountNumber(string accountNumber)
        {

            if (String.IsNullOrEmpty(accountNumber.Trim())) throw new Exception("Account number is required to get account");

            var response = await _webService.Client.GetAsync($"api/Account/GetByAccountNumber/{accountNumber}");
            var data = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return null;

            return ConvertJsonToAccount(data);
        }

        public async Task<List<Account>> GetByCustomerId(int id)
        {
            if (id <= 0) throw new Exception("Customer id is required to get accounts bound to customer");

            var response = await _webService.Client.GetAsync($"api/Account/GetByCustomerId/{id}");

            var data = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return null;

            return ConvertJsonToAccounts(data);
        }

        public async Task<Account> GetByMeterNumber(string meterNumber)
        {

            var response = await _webService.Client.GetAsync($"api/Account/GetByMeterNumber/{meterNumber}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToAccount(data);
        }

        #endregion

    }
}
