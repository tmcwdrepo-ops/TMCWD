using Microsoft.AspNetCore.Mvc;
using TMCWD.CustomerSupport;
using TMCWD.Model.CustomerSupport;

namespace TMCWD.Application.Controllers
{
    public class ApprovalController : Controller
    {
        private readonly ApprovalHistoryTransaction _approvalHistoryTransaction;

        public ApprovalController(ApprovalHistoryTransaction approvalHistoryTransaction)
        {
            _approvalHistoryTransaction = approvalHistoryTransaction;
        }

        public async Task<IActionResult> GetHistory(int jobOrderId)
        {
            var history = await _approvalHistoryTransaction.GetAll(jobOrderId);
            if (history == null || !history.Any()) return NoContent();
            return Ok(history);
        }

    }
}
