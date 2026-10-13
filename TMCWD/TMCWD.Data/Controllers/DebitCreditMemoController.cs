using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DebitCreditMemoController : ControllerBase
    {
        private readonly IDebitCreditMemoService _service;

        public DebitCreditMemoController(IDebitCreditMemoService service)
        {
            _service = service;
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "PendingAuthorizationDecision")]
        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(long id)
        {
            var memo = await _service.Get(id);
            return memo == null ? NotFound() : Ok(memo);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "PendingAuthorizationDecision")]
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll() => Ok(await _service.GetAll());

        [HttpPost("Save/{userId}")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "PendingAuthorizationDecision")]
        public async Task<IActionResult> Save(int userId, [FromBody] DebitCreditMemo memo)
        {
            if (memo == null || string.IsNullOrWhiteSpace(memo.AccountNumber)
                || string.IsNullOrWhiteSpace(memo.DcmType)
                || (memo.MemoType != "debit" && memo.MemoType != "credit")
                || memo.Amount <= 0 || string.IsNullOrWhiteSpace(memo.Explanation))
                return BadRequest("A valid account, memo type, amount, and explanation are required.");

            return Ok(await _service.Save(userId, memo));
        }
    }
}
