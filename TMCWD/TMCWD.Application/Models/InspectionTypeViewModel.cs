using TMCWD.Model.Administrator;

namespace TMCWD.Application.Models
{
    public class InspectionTypeViewModel
    {

        public InspectionTypeViewModel() { }

        public User CurrentUser { get; set; } = new User();

        public InspectionType AddEditInspectionType { get; set; } = new InspectionType();

        public List<InspectionType> InspectionTypes { get; set; } = new List<InspectionType>();

        public string SearchString { get; set; } = string.Empty;

    }
}
