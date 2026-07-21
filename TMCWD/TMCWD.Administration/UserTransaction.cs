using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Net.Http.Json;
using System.Text.Json;
using TMCWD.Model.Administrator;
using TMCWD.Model.Interfaces;
using TMCWD.Utility.Generic;
using TMCWD.Utility.Encryption;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text;
using System.Net.WebSockets;
using TMCWD.Services;

namespace TMCWD.Administration
{
    public class UserTransaction
    {

        #region fields

        WebService _webService;

        #endregion

        #region constructors

        public UserTransaction(WebService webService) { _webService = webService; }

        #endregion

        #region public methods

        public User ConvertJsonStringToUser(string jsonString)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<User>(jsonString, serializerOptions) ?? new User();
        }

        public List<User> ConvertJsonStringToUsers(string jsonString)
        {
            var serializerOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<User>>(jsonString, serializerOptions) ?? new List<User>();
        }

        public async Task<User?> SaveUpdate(int userId, User user)
        {
            var content = JsonContent.Create(user);
            var response = await _webService.Client.PostAsync($"api/Users/SaveUpdate/{userId}", content);
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return this.ConvertJsonStringToUser(data);
        }

        public async Task<User> Get(int id)
        {
            var response = await _webService.Client.GetAsync($"api/Users/Get/{id}");
            var data = await response.Content.ReadAsStringAsync();
            if(!response.IsSuccessStatusCode) return null;
            return this.ConvertJsonStringToUser(data);
        }

        public async Task<User> GetByName(string name)
        {
            var response = await _webService.Client.GetAsync($"api/Users/GetByName/{name}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonStringToUser(data);
        }

        public async Task<User> GetUserByEmail(string email)
        {
            var response = await _webService.Client.GetAsync($"api/Users/GetByEmail/{email}");
            var data = await response.Content.ReadAsStringAsync();
            if(!response.IsSuccessStatusCode) return null;
            return ConvertJsonStringToUser(data);
        }

        public async Task<List<User>> GetUsers()
        {

            var response = await _webService.Client.GetAsync("api/Users/GetUsers");

            var data = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return null;

            return ConvertJsonStringToUsers(data);
        }

        public async Task<bool> ChangePassword(int userId, int id, string currentPassword, string newPassword, string confirmPassword)
        {
            StringBuilder sb = new();
            var user = await this.Get(id);

            currentPassword = StringEncyption.Encrypt(currentPassword);
            newPassword = StringEncyption.Encrypt(newPassword);
            confirmPassword = StringEncyption.Encrypt(confirmPassword);

            if (user == null) throw new Exception("Change password failed, user not found");

            if (user.Password != currentPassword) sb.AppendLine("Current password mismatch");
            if (newPassword != confirmPassword) sb.AppendLine("New password confirmation failed");

            if (String.IsNullOrEmpty(sb.ToString().Trim())) throw new Exception(sb.ToString());

            user.Password = newPassword;

            user = await this.SaveUpdate(userId, user);


            return user != null && user.Id > 0;
        }

        public List<SelectListItem> GetRoles()
        {
            return new List<SelectListItem>()
            {
                { new SelectListItem(nameof(UserRole.Engineer), ((int)UserRole.Engineer).ToString()) },
                { new SelectListItem(nameof(UserRole.SuperAdmin), ((int)UserRole.SuperAdmin).ToString()) },
                { new SelectListItem(nameof(UserRole.Guest), ((int)UserRole.Guest).ToString()) },
                { new SelectListItem(nameof(UserRole.CustomerRepresentative), ((int)UserRole.CustomerRepresentative).ToString()) }
            };
        }

        public async Task<List<User>> SearchUser(string searchString)
        {

            var response = await _webService.Client.GetAsync($"api/Users/SearchUser/{searchString}");

            var data = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonStringToUsers(data);
        }

        public async Task<List<User>> GetUsersByRole(UserRole role)
        {
            var response = await _webService.Client.GetAsync($"api/User/GetUsersByClient/{(int)role}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonStringToUsers(data);
        }

        #endregion

    }
}
