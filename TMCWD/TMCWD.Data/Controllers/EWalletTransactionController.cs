using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EWalletTransactionController : Controller
    {
        #region fields

        private readonly IEWalletTransactionService _service;

        #endregion

        #region constructors

        public EWalletTransactionController(IEWalletTransactionService service)
        {
            _service = service;
        }

        #endregion

        #region methods

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "PendingAuthorizationDecision")]
        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var eWalletTransaction = await _service.Get(id);
            if (eWalletTransaction == null) return NotFound();
            return Ok(eWalletTransaction);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "PendingAuthorizationDecision")]
        [HttpGet("GetByPaymentReference/{reference}")]
        public async Task<IActionResult> GetByPaymentReference(string reference)
        {
            var eWalletTransactions = await _service.GetByPaymentReference(reference);
            if (eWalletTransactions == null || !eWalletTransactions.Any()) return NotFound();
            return Ok(eWalletTransactions);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "PendingAuthorizationDecision")]
        [HttpGet("GetByProcessedBy/{processBy}")]
        public async Task<IActionResult> GetByProcessedBy(int processedBy)
        {
            var eWalletTransactions = await _service.GetByProcessedBy(processedBy);
            if (eWalletTransactions == null || !eWalletTransactions.Any()) return NotFound();
            return Ok(eWalletTransactions);
        }

        [HttpPost("SaveUpdate/{userId}")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "PendingAuthorizationDecision")]
        public async Task<IActionResult> SaveUpdate(int userId, EWalletTransaction transaction)
        {
            var savedTransaction = await _service.SaveUpdate(userId, transaction);
            return Ok(savedTransaction);
        }

        #endregion

    }
}
