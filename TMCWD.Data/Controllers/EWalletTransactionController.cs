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

        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var eWalletTransaction = await _service.Get(id);
            if (eWalletTransaction == null) return NotFound();
            return Ok(eWalletTransaction);
        }

        [HttpGet("GetByPaymentReference/{reference}")]
        public async Task<IActionResult> GetByPaymentReference(string reference)
        {
            var eWalletTransactions = await _service.GetByPaymentReference(reference);
            if (eWalletTransactions == null || !eWalletTransactions.Any()) return NotFound();
            return Ok(eWalletTransactions);
        }

        [HttpGet("GetByProcessedBy/{processBy}")]
        public async Task<IActionResult> GetByProcessedBy(int processedBy)
        {
            var eWalletTransactions = await _service.GetByProcessedBy(processedBy);
            if (eWalletTransactions == null || !eWalletTransactions.Any()) return NotFound();
            return Ok(eWalletTransactions);
        }

        [HttpGet("SaveUpdate/{userId}")]
        public async Task<IActionResult> SaveUpdate(int userId, EWalletTransaction transaction)
        {
            var savedTransaction = await _service.SaveUpdate(userId, transaction);
            return Ok(savedTransaction);
        }

        #endregion

    }
}
