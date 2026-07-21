using TMCWD.Model.Administrator;
namespace TMCWD.Application.Models
{
    public class RequestInspectionTypeViewModel
    {

        public RequestInspectionTypeViewModel() { }

        public InspectionType Type { get; set; } = new InspectionType();

        public bool IsChecked { get; set; } = false;
    }
}
