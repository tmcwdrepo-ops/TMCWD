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
            if (account.Id > 0 && String.IsNullOrEmpty(account.AccountNumber.Trim())) sb.AppendLine("Account number is required");
            if (account.Id > 0 && String.IsNullOrEmpty(account.MeterNumber.Trim())) sb.AppendLine("Meter number is required");
            if (account.CustomerId <= 0) sb.AppendLine("No customer has been selected for this account");
            if (String.IsNullOrEmpty(account.HouseNumber.Trim())) sb.AppendLine("Account house number is required for account creation");

            if (!String.IsNullOrEmpty(sb.ToString().Trim())) throw new Exception(sb.ToString());

            if (account.Id > 0)
            {
                var stored = await Get(account.Id) ?? throw new InvalidOperationException("Account not found.");
                if ((account.Status != 0 && account.Status != stored.Status)
                    || (account.ZoneBookId != 0 && account.ZoneBookId != stored.ZoneBookId)
                    || (account.Sequence != 0 && account.Sequence != stored.Sequence)
                    || (!string.IsNullOrEmpty(account.AccountNumber) && account.AccountNumber != stored.AccountNumber)
                    || account.InitialReading != stored.InitialReading)
                    throw new InvalidOperationException("Account status, numbering and roster changes require an approved workflow.");
            }
            var content = JsonContent.Create(new AccountWriteRequest { Id = account.Id, CustomerId = account.CustomerId,
                MeterNumber = account.MeterNumber, MeterSize = account.MeterSize, Classification = (int)account.Classification,
                UnitNumber = account.UnitNumber, Building = account.Building, HouseNumber = account.HouseNumber,
                Street = account.Street, Barangay = account.Barangay, IsCurrentAddress = account.IsCurrentAddress });
            var response = await _webService.Client.PostAsync($"api/Account/SaveUpdate/{userId}", content);

            var data = await response.Content.ReadAsStringAsync();

            response.EnsureSuccessStatusCode();

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

        public async Task<List<Account>> GetByZoneAndBook(int zone, int book)
        {
            var response = await _webService.Client.GetAsync($"api/Account/GetByZoneAndBook/{zone}/{book}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToAccounts(data);
        }

        public async Task<List<Account>> GetByZoneBookId(int zoneBookId)
        {
            var response = await _webService.Client.GetAsync($"api/Account/GetByZoneBookId/{zoneBookId}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToAccounts(data);
        }

        public async Task<List<Account>> GetAccounts()
        {
            var response = await _webService.Client.GetAsync("api/Account/GetAll");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToAccounts(data);
        }

        public async Task<List<Account>> GetByZoneBookAndSequence(int zone, int book, int seqFrom, int seqTo)
        {
            var response = await _webService.Client.GetAsync($"api/Account/GetByZoneBookAndSequence/{zone}/{book}/{seqFrom}/{seqTo}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonToAccounts(data);
        }

        public async Task<List<Account>> Search(string query)
        {
            var response = await _webService.Client.GetAsync($"api/Account/Search?q={Uri.EscapeDataString(query)}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return new List<Account>();
            return ConvertJsonToAccounts(data);
        }

        #endregion

    }

}
