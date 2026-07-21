using TMCWD.Model.Administrator;

namespace TMCWD.Application.Models
{
    public class OtherFeeTypeViewModel
    {
        public OtherFeeType AddEditOtherViewType { get; set; } = new OtherFeeType();

        public List<OtherFeeType> OtherFeeTypes { get; set; } = new List<OtherFeeType>();
    }
}
