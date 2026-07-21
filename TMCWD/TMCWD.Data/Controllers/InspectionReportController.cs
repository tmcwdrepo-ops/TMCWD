using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;
using TMCWD.Utility.Generic;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InspectionReportController : Controller
    {

        private readonly IInspectionReportService _inspectionReportService;

        public InspectionReportController(IInspectionReportService service)
        {
            _inspectionReportService = service;
        }

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<ActionResult> SaveUpdate(int userId, [FromBody] InspectionReport report)
        {
            var updatedReport = await _inspectionReportService.SaveUpdate(userId, report);
            if (updatedReport == null) NotFound();
            return Ok(updatedReport);
        }

        [HttpGet("Get/{id}")]
        public async Task<ActionResult> Get(int id)
        {
            var report = await _inspectionReportService.Get(id);
            if(report == null) NotFound();
            return Ok(report);
        }

        [HttpGet("GetByRequestId/{requestId}")]
        public async Task<ActionResult> GetByRequestId(int requestId)
        {
            var reports = await _inspectionReportService.GetByRequestId(requestId);
            if (reports == null || !reports.Any()) return NotFound();
            return Ok(reports);
        }

    }
}
