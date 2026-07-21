using TMCWD.CustomerSupport;
using TMCWD.Model.CustomerSupport;
using TMCWD.Model.Administrator;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TMCWD.Application.Models
{
    public class AccountViewModel
    {

        public AccountViewModel() { }

        public User CurrentUser { get; set; } = new();

        public Customer Customer { get; set; } = new();

        public int CustomerId { get; set; }

        public Account AddEditAccount { get; set; } = new();

        public List<Account> PagedAccountList { get; set; } = new();

        public List<SelectListItem> AccountStatuses { get; set; } = new List<SelectListItem>() 
        { 
            new SelectListItem { Text = AccountStatus.Pending.ToString(), Value = $"{(int)AccountStatus.Pending}" },
            new SelectListItem { Text = AccountStatus.Active.ToString(), Value = $"{(int)AccountStatus.Active}" },
            new SelectListItem { Text = AccountStatus.Suspended.ToString(), Value = $"{(int)AccountStatus.Suspended}" },
            new SelectListItem { Text = AccountStatus.Closed.ToString(), Value = $"{(int)AccountStatus.Closed}" }
        };

    }
}
