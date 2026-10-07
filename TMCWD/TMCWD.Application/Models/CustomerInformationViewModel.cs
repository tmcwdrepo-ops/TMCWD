using Microsoft.AspNetCore.Mvc.Rendering;
using TMCWD.Model.CustomerSupport;

namespace TMCWD.Application.Models
{
    public class CustomerInformationViewModel
    {
        // ── Customer ─────────────────────────────────────────────────────────
        public Customer Customer { get; set; } = new();

        /// <summary>
        /// The account currently selected / being viewed.
        /// </summary>
        public Account? SelectedAccount { get; set; }

        /// <summary>
        /// All accounts belonging to the current customer.
        /// </summary>
        public List<Account> CustomerAccounts { get; set; } = new();

        // ── Navigation context ───────────────────────────────────────────────
        /// <summary>
        /// Sorted list of all customer IDs — used for Prev / Next navigation.
        /// </summary>
        public List<int> CustomerIds { get; set; } = new();

        public int CurrentIndex => CustomerIds.IndexOf(Customer.Id);
        public int? PreviousCustomerId => CurrentIndex > 0 ? CustomerIds[CurrentIndex - 1] : null;
        public int? NextCustomerId => CurrentIndex >= 0 && CurrentIndex < CustomerIds.Count - 1
            ? CustomerIds[CurrentIndex + 1]
            : null;

        // ── Select lists ─────────────────────────────────────────────────────
        public List<SelectListItem> AccountClassifications { get; set; } = Enum
            .GetValues<AccountClassification>()
            .Select(e => new SelectListItem { Text = e.ToString(), Value = ((int)e).ToString() })
            .ToList();

        public List<SelectListItem> AccountStatuses { get; set; } = new()
        {
            new SelectListItem { Text = AccountStatus.Pending.ToString(),   Value = $"{(int)AccountStatus.Pending}"   },
            new SelectListItem { Text = AccountStatus.Active.ToString(),    Value = $"{(int)AccountStatus.Active}"    },
            new SelectListItem { Text = AccountStatus.Suspended.ToString(), Value = $"{(int)AccountStatus.Suspended}" },
            new SelectListItem { Text = AccountStatus.Closed.ToString(),    Value = $"{(int)AccountStatus.Closed}"    },
        };
    }
}
