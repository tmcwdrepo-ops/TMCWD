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
        public int LastFailureStatusCode { get; private set; }
        public string? SecurityStamp { get; private set; }

        #endregion

        #region methods

        public async Task<User> Login()
        {
            SecurityStamp = null;
            if (String.IsNullOrEmpty(this.Email.Trim()) || String.IsNullOrEmpty(this.Password.Trim()))
                return new User();

            LastFailureStatusCode = 0;
            using var response = await _webService.Client.PostAsJsonAsync("api/auth/login",
                new PasswordLoginRequest { Identifier = Email.Trim(), Password = Password });

            // Error responses from the API contain a JSON string/message, not a User object.
            // Do not pass those bodies to the User deserializer.
            if (!response.IsSuccessStatusCode)
            {
                LastFailureStatusCode = (int)response.StatusCode;
                return new User();
            }

            var data = await response.Content.ReadAsStringAsync();
            if (String.IsNullOrWhiteSpace(data))
                return new User();

            var currentUser = ConvertJsonToUser(data);
            using var login = JsonDocument.Parse(data);
            SecurityStamp = login.RootElement.TryGetProperty("securityStamp", out var stamp) ? stamp.GetString() : null;
            if (currentUser.Id == 0 || string.IsNullOrWhiteSpace(SecurityStamp)) return new User();

            return currentUser;
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
