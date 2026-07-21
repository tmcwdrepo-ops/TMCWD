using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;
using TMCWD.Model.Administrator.Interface;
using TMCWD.Utility.Generic;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InspectionTypeController : Controller
    {

        private readonly IInspectionTypeService _inspectionTypeService;

        public InspectionTypeController(IInspectionTypeService service)
        {
            _inspectionTypeService = service;
        }

        [HttpGet("GetTypes")]
        public async Task<ActionResult> GetTypes()
        {
            var inspectionTypes = await _inspectionTypeService.GetTypes();

            if (inspectionTypes == null || !inspectionTypes.Any()) return NotFound("No inspection type(s) found.");

            return Ok(inspectionTypes);
        }

        [HttpGet("Get/{id}")]
        public async Task<ActionResult> Get(int id)
        {
            var inspectionType = await _inspectionTypeService.Get(id);

            if (inspectionType == null) return NotFound($"Inspection type with id {id} was not found.");

            return Ok(inspectionType);
        }

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<ActionResult> SaveUpdate(int userId, [FromBody] InspectionType type)
        {
            StringBuilder sb = new();

            if (String.IsNullOrEmpty(type.Name.Trim())) return BadRequest("Name is required in creating inspection type.");

            var updatedType = await _inspectionTypeService.SaveUpdate(userId, type);

            if (updatedType == null || updatedType?.Id <= 0) return BadRequest("Problem(s) were encountered while saving inspection type");

            return Ok(updatedType);
        }

        [HttpGet("GetNewRequestType")]
        public async Task<ActionResult> GetNewRequestType()
        {
            var inspectionType = await _inspectionTypeService.GetNewRequestType();
            if(inspectionType == null) return NotFound("Inspection type for new request is not found");

            return Ok(inspectionType);
        } 

    }
}
