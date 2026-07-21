using Microsoft.AspNetCore.Mvc.Rendering;
using TMCWD.Model.Administrator;

namespace TMCWD.Application.Models
{
    public class AdminViewModel
    {
        public AdminViewModel() { }

        public List<User> PagedUserList { get; set; } = new List<User>();

        public User AddEditUser { get; set; } = new User();

        public string Password { get; set; } = string.Empty;

        public string ConfirmPassword { get; set; } = string.Empty;

        public List<SelectListItem> Roles { get; set; } = new List<SelectListItem>();

        public string SearchString { get; set; } = string.Empty;

    }
}
