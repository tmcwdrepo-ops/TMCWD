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
            var content = JsonContent.Create(new UserWriteRequest { Id = user.Id,
                Name = user.Name ?? "", Email = user.Email ?? "", Role = user.Role,
                IsActive = user.IsActive, Password = user.Id == 0 ? user.Password : null });
            var response = await _webService.Client.PostAsync($"api/Users/SaveUpdate/{userId}", content);
            var data = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();
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
            if (userId != id) throw new InvalidOperationException("Administrative password reset requires an approved policy.");
            using var response = await _webService.Client.PostAsJsonAsync("api/Users/password",
                new PasswordChangeRequest { CurrentPassword = currentPassword, NewPassword = newPassword,
                    ConfirmPassword = confirmPassword });
            response.EnsureSuccessStatusCode();
            return true;
        }

        public List<SelectListItem> GetRoles()
        {
            return new List<SelectListItem>()
            {
                new SelectListItem("Super Admin", ((int)UserRole.SuperAdmin).ToString()),
                new SelectListItem("Customer Representative", ((int)UserRole.CustomerRepresentative).ToString()),
                new SelectListItem("Engineer", ((int)UserRole.Engineer).ToString()),
                new SelectListItem("Meter Reader", ((int)UserRole.MeterReader).ToString()),
                new SelectListItem("Guest", ((int)UserRole.Guest).ToString()),
                new SelectListItem("Customer Service OIC", ((int)UserRole.CustomerServiceOic).ToString()),
                new SelectListItem("Billing", ((int)UserRole.Billing).ToString()),
                new SelectListItem("Billing Head", ((int)UserRole.BillingHead).ToString()),
                new SelectListItem("General Services", ((int)UserRole.GeneralServices).ToString()),
                new SelectListItem("Accounting", ((int)UserRole.Accounting).ToString())
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
            var response = await _webService.Client.GetAsync($"api/Users/GetUsersByRole/{(int)role}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonStringToUsers(data);
        }

        public async Task<List<User>> GetUsersById(List<int> ids)
        {
            string queryParam = string.Join("&", ids.Select(x => $"ids={x}"));
            var response = await _webService.Client.GetAsync($"api/Users/GetUsersById?{queryParam}");
            var data = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return ConvertJsonStringToUsers(data);
        }

        #endregion

    }
}
