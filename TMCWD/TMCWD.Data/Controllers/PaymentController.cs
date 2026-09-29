using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : Controller
    {

        #region fields

        private readonly IPaymentService _service;

        #endregion

        #region constructors

        public PaymentController(IPaymentService service)
        {
            _service = service;
        }

        #endregion

        #region methods

        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var payment = await _service.Get(id);
            if (payment == null) return NotFound();
            return Ok(payment);
        }

        [HttpGet("GetByBillingReference/{referenceId}")]
        public async Task<IActionResult> GetByBillingReference(string referenceId)
        {
            var payments = await _service.GetByBillingReference(referenceId);
            if(payments == null || !payments.Any()) return NotFound();
            return Ok(payments);
        }

        [HttpGet("GetByCashier/{userId}")]
        public async Task<IActionResult> GetByCashier(int userId)
        {
            var payments = await _service.GetByCashier(userId);
            if (payments == null || !payments.Any()) return NotFound();
            return Ok(payments);
        }

        [HttpGet("GetByMethod/{methodId}")]
        public async Task<IActionResult> GetByMethod(int methodId)
        {
            var payments = await _service.GetByMethod(methodId);
            if (payments == null || !payments.Any()) return NotFound();
            return Ok(payments);
        }

        [HttpGet("GetByMacAddress/{macAddress}")]
        public async Task<IActionResult> GetByMacAddress(string macAddress)
        {
            var payments = await _service.GetByMacAddress(macAddress);
            if (payments == null || !payments.Any()) return NotFound();
            return Ok(payments);
        }

        [HttpGet("GetByPaymentReference/{referenceId}")]
        public async Task<IActionResult> GetByPaymentReference(string referenceId)
        {
            var payment = await _service.GetByPaymentReference(referenceId);
            if (payment == null) return NotFound();
            return Ok(payment);
        }

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<IActionResult> SaveUpdate(int userId, Payment payment)
        {
            var savedPayment = await _service.SaveUpdate(userId, payment);
            return Ok(savedPayment);
        }

        [HttpPost("ProcessCollection/{userId}")]
        public async Task<IActionResult> ProcessCollection(
            int userId,
            [FromBody] TMCWD.Model.Billing.Requests.CollectionPaymentRequest request)
        {
            try
            {
                var payments = await _service.ProcessCollectionPayment(userId, request);
                return Ok(new { success = true, paymentCount = payments.Count });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("SearchInvoices")]
        public async Task<IActionResult> SearchInvoices([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q))
                return Ok(Array.Empty<TMCWD.Model.Billing.InvoiceRecord>());
            return Ok(await _service.SearchInvoices(q));
        }

        /// <summary>
        /// Returns all successful payments within an inclusive date range, optionally filtered
        /// by the cashier who created them.  Used for collection reporting.
        /// </summary>
        [HttpGet("GetByDateRange")]
        public async Task<IActionResult> GetByDateRange(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            [FromQuery] int cashierId = 0)
        {
            var payments = await _service.GetByDateRange(from, to, cashierId);
            return Ok(payments);
        }

        [HttpGet("Invoice/{invoiceNumber}")]
        public async Task<IActionResult> Invoice(string invoiceNumber)
        {
            var invoice = await _service.GetInvoice(invoiceNumber);
            return invoice == null ? NotFound() : Ok(invoice);
        }

        [HttpPost("CancelInvoice/{userId}")]
        public async Task<IActionResult> CancelInvoice(
            int userId,
            [FromBody] TMCWD.Model.Billing.CancelInvoiceRequest request)
        {
            try
            {
                await _service.CancelInvoice(userId, request);
                return Ok(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        #endregion

    }
}
