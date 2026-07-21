using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{

    [ApiController]
    [Route("api/{jobOrderId}/[controller]")]
    public class ApprovalHistoryController : Controller
    {
        private readonly IApprovalHistoryService _service;

        public ApprovalHistoryController(IApprovalHistoryService service)
        {
            _service = service;
        }

        [HttpPost("Save/{userId}")]
        public async Task<IActionResult> Save(int userId, int jobOrderId, ApprovalHistory history)
        {

            var updatedApprovalHistory = await _service.Save(userId, jobOrderId, history);
            if (updatedApprovalHistory == null) return NoContent();

            return Ok(updatedApprovalHistory);
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll(int jobOrderId)
        {
            var histories = await _service.GetAll(jobOrderId);
            if (histories == null || !histories.Any()) return NotFound();
            return Ok(histories);
        }

    }
}
