using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{

    [ApiController]
    [Route("api/{jobOrderId}/[controller]")]
    public class MaterialController : Controller
    {
        private readonly IMaterialService _service;

        public MaterialController(IMaterialService service)
        {
            _service = service;
        }

        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var material = await _service.Get(id);

            if (material == null) return NotFound();

            return Ok(material);
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var materials = await _service.GetAll();
            if (materials == null || !materials.Any()) return NotFound();
            return Ok(materials);
        }

        [HttpGet("GetByJobOrderId")]
        public async Task<IActionResult> GetByJobOrderId(int jobOrderId)
        {
            var materials = await _service.GetByJobOrderId(jobOrderId);
            if(materials == null || !materials.Any()) return NotFound();
            return Ok(materials);
        }

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<IActionResult> SaveUpdate(int userId, int jobOrderId, [FromBody] Material material)
        {
            var updatedMaterial = await _service.SaveUpdate(userId, jobOrderId, material);
            if (material == null) return NoContent();
            return Ok(updatedMaterial);
        }

        [HttpPut("UpdateQuantityOrNewUnitCost/{userId}")]
        public async Task<IActionResult> UpdateQuantityOrNewUnitCost(int userId, int requestId, [FromBody]Material updateMaterial)
        {
            var forUpdate = await _service.Get((int)updateMaterial.Id);

            if (forUpdate == null) return NoContent();

            if(forUpdate.RequestedQuantity != updateMaterial.RequestedQuantity) forUpdate.RequestedQuantity = updateMaterial.RequestedQuantity;
            if(forUpdate.UnitSellingPrice != updateMaterial.UnitSellingPrice) forUpdate.UnitSellingPrice = updateMaterial.UnitSellingPrice;

            var material = await _service.UpdateQuantityOrNewUnitCost(userId, requestId, forUpdate);
            if(material == null) return NotFound();
            return Ok(material);
        }

        [HttpDelete("Delete/{id}")]
        public async Task<IActionResult> DeleteMaterial(int id)
        {
            var material = await _service.Get(id);
            if (material == null) return NotFound();
            var res = await _service.Delete(material);
            return Ok(res);
        }

    }

}
