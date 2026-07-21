using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/{jobOrderId}/[controller]")]
    public class FindingController : Controller
    {

        private readonly IFindingService _service;

        public FindingController(IFindingService service)
        {
            _service = service;
        }

        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var finding = await _service.Get(id);

            if (finding == null) return NotFound();

            return Ok(finding);
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll(int jobOrderId)
        {

            var findings = await _service.GetAll(jobOrderId);

            if (findings == null) return NotFound();

            return Ok(findings);
        }

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<IActionResult> SaveUpdate(int userId, int jobOrderId, Finding finding)
        {
            var updatedFinding = await _service.SaveUpdate(userId, jobOrderId, finding);
            if(updatedFinding == null) return NotFound();
            return Ok(updatedFinding);
        }

    }
}
