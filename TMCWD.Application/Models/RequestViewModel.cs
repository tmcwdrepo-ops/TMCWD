using Microsoft.AspNetCore.Mvc.Rendering;
using TMCWD.CustomerSupport;
using TMCWD.Model.Administrator;
using TMCWD.Model.CustomerSupport;
using TMCWD.Model.Engineering;
namespace TMCWD.Application.Models
{
    public class RequestViewModel
    {

        public User CurrentUser { get; set; } = new User();

        public string SearchString { get; set; } = string.Empty;

        public Request AddEditRequest { get; set; } = new Request();

        public List<Request>Requests { get; set; } = new List<Request>();

        public List<RequestInspectionTypeViewModel> InspectionTypes { get; set; } = new List<RequestInspectionTypeViewModel>();

        public Customer CurrentCustomer { get; set; } = new Customer();

        public Account CurrentAccount { get; set; } = new Account();

        public List<RequestDetail> CurrentRequestDetails { get; set; } = new List<RequestDetail>();

        public IEnumerable<SelectListItem> AccountSelectionList { get; set; } = new List<SelectListItem>();

        public List<Recommendation> Recommendations { get; set; } = new List<Recommendation>();

        public List<Inventory> InventoryItems { get; set; } = new List<Inventory>();

        public List<MaterialDetailViewModel> Materials { get; set; } = new List<MaterialDetailViewModel>();

        public List<JobOrder> JobOrders { get; set; } = new List<JobOrder>();

    }
}
