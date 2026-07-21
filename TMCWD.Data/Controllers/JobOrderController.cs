using Microsoft.AspNetCore.Mvc;
using System.Runtime.CompilerServices;
using TMCWD.Data.Services;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/{requestId}/[controller]")]
    public class JobOrderController : Controller
    {
        private readonly IJobOrderService _service;

        public JobOrderController(IJobOrderService service)
        {
            _service = service;
        }

        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var jobOrder = await _service.Get(id);
            if (jobOrder == null) return NotFound();
            return Ok(jobOrder);
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll(int requestId)
        {
            var jobOrders = await _service.GetAll(requestId);
            if(jobOrders == null || !jobOrders.Any()) return NotFound();
            return Ok(jobOrders);
        }

        [HttpGet("GetByRequestId")]
        public async Task<IActionResult> GetByRequestId(int requestId)
        {
            var jobOrders = await _service.GetByRequestId(requestId);
            if(jobOrders == null || !jobOrders.Any()) return NotFound();
            return Ok(jobOrders);
        }

        [HttpGet("GetByRequestDetailId/{requestDetailId}")]
        public async Task<IActionResult> GetByRequestDetailId(int requestDetailId)
        {
            var jobOrders = await _service.GetByRequestDetailId(requestDetailId);
            if (jobOrders == null) return NotFound();
            return Ok(jobOrders);
        }

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<IActionResult> SaveUpdate(int userId, [FromBody] JobOrder jobOrder)
        {var updatedJobOrder = await _service.SaveUpdate(userId, jobOrder);
            if (updatedJobOrder == null) return NoContent();
            return Ok(updatedJobOrder);
        }

    }
}
