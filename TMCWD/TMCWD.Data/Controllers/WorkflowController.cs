using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Services;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WorkflowController : Controller
    {

        private readonly IWorkflowService _service;

        public WorkflowController(IWorkflowService service)
        {
            _service  = service;
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "PendingAuthorizationDecision")]
        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var workflow = await _service.Get(id);
            if (workflow == null) return NotFound();
            return Ok(workflow);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "PendingAuthorizationDecision")]
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var workflows = await _service.GetAll();
            if(workflows == null || !workflows.Any()) return NotFound();
            return Ok(workflows);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "PendingAuthorizationDecision")]

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<IActionResult> SaveUpdate(int userId, [FromBody] Workflow workflow)
        {
            var updatedWorkflow = await _service.SaveUpdate(userId, workflow);
            if (updatedWorkflow == null) return NoContent();
            return Ok(updatedWorkflow);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Policy = "PendingAuthorizationDecision")]

        [HttpDelete("Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var workflow = await _service.Get(id);
            if(workflow == null) return NotFound();
            bool isSuccess = await _service.Delete(workflow);
            return Ok(isSuccess);
        }

    }
}
