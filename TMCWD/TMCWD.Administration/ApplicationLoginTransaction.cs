using System;
using System.Net.Http.Json;
using TMCWD.Utility.Generic;
using TMCWD.Utility.Encryption;
using TMCWD.Model.Administrator;
using TMCWD.Model.Interfaces;
using TMCWD.Services;
using Microsoft.AspNetCore.WebUtilities;
using System.Text.Json;


namespace TMCWD.Administration
{
    public class ApplicationLoginTransaction : TransactionBase
    {

        #region fields

        private readonly WebService _webService;

        #endregion


        #region constructors

        /// <summary>
        /// Initializes a new instance of the ApplicationLogin class.`
        /// </summary>
        public ApplicationLoginTransaction(WebService webService) 
        {
            _webService = webService;
        }

        #endregion

        #region properties

        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        #endregion

        #region methods

        public async Task<User> Login()
        {
            if (String.IsNullOrEmpty(this.Email.Trim()) || String.IsNullOrEmpty(this.Password.Trim()))
                return new User();

            var response = await _webService.Client.GetAsync($"api/Users/GetByEmail/{this.Email.Trim()}");

            var data = await response.Content.ReadAsStringAsync();

            var currentUser = ConvertJsonToUser(data);
            if (currentUser.Id == 0) throw new Exception("User not found");

            if (currentUser.Password == StringEncyption.Encrypt(this.Password)) return currentUser;

            return new();
        }

        public User ConvertJsonToUser(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<User>(json, serializerOptions) ?? new();
        }

        public List<User> ConvertJsonToUsers(string json)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<User>>(json, serializerOptions) ?? new();
        }

        #endregion
    }
}
