namespace TMCWD.Application.Models
{
    public class FindingViewModel
    {

        public FindingViewModel() { }

        public int JobOrderId { get; set; }

        public int RequestId { get; set; }

        public string Details { get; set; } = string.Empty;

        public JobOrderStatus Status { get; set; }

        public List<IFormFile> FindingFiles { get; set; } = new();

    }
}
