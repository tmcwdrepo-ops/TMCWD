using TMCWD.Model.CustomerSupport;

namespace TMCWD.Application.Models
{
    public class CustomerComponentViewModel
    {

        public Customer Customer { get; set; } = new Customer();

        public string ParagraphClass { get; set; } = string.Empty;

        public string ParagraphId { get; set; } = string.Empty;

    }
}
