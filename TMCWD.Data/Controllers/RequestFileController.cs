using Microsoft.AspNetCore.Mvc;
using System.Text;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class RequestFileController : Controller
    {
        private readonly IRequestFileService _service;

        public RequestFileController(IRequestFileService service)
        {
            _service = service;
        }

        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var requestFile = await _service.Get(id);
            if (requestFile == null) return NotFound();
            return Ok(requestFile);
        }

        [HttpGet("GetAll/{jobOrderId}")]
        public async Task<IActionResult> GetAll(int jobOrderId)
        {
            var requestFiles = await _service.GetAll(jobOrderId);
            if (requestFiles == null || !requestFiles.Any()) return NotFound();
            return Ok(requestFiles);
        }

        [HttpPost("SaveUpdate/{userId}/{jobOrderId}")]
        public async Task<IActionResult> SaveUpdate(int userId, int jobOrderId, RequestFile file)
        {
            StringBuilder sb = new();

            if (file == null) throw new Exception("File details is not supplied");

            if (String.IsNullOrEmpty(file.OriginalFilename.Trim())) sb.AppendLine("Original filename is not supplied");

            var updateFile = await _service.SaveUpdate(userId, jobOrderId, file);
            if (updateFile == null) return NoContent();
            return Ok(updateFile);
        }

        [HttpPost("SaveRange")]
        public async Task<IActionResult> SaveRange(RequestFile[] files)
        {
            var updatedFiles = await _service.SaveRange(files);
            if (updatedFiles == null || !updatedFiles.Any()) return NoContent();
            return Ok(updatedFiles);
        }

    }
}
