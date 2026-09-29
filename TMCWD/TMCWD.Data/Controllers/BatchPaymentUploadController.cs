using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BatchPaymentUploadController : ControllerBase
    {
        private readonly IBatchPaymentUploadService _service;

        public BatchPaymentUploadController(IBatchPaymentUploadService service)
        {
            _service = service;
        }

        /// <summary>List uploads within an inclusive date range.</summary>
        [HttpGet("GetByDateRange")]
        public async Task<IActionResult> GetByDateRange(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to)
        {
            var uploads = await _service.GetByDateRange(from, to);
            return Ok(uploads);
        }

        /// <summary>Get a single upload record by ID.</summary>
        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(long id)
        {
            var upload = await _service.Get(id);
            return upload == null ? NotFound() : Ok(upload);
        }

        /// <summary>Record a new CSV upload.</summary>
        [HttpPost("Save/{userId}")]
        public async Task<IActionResult> Save(
            int userId,
            [FromBody] BatchPaymentUpload upload)
        {
            if (upload == null || string.IsNullOrWhiteSpace(upload.Filename))
                return BadRequest("Filename is required.");

            var saved = await _service.Save(userId, upload);
            return Ok(saved);
        }
    }
}
