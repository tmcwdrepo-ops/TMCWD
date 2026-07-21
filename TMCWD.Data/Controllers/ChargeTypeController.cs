using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;

namespace TMCWD.Data.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class ChargeTypeController : Controller
    {

        #region fields

        private readonly IChargeTypeService _service;

        #endregion

        #region constructors

        public ChargeTypeController(IChargeTypeService service)
        {
            _service = service;
        }

        #endregion

        #region methods

        [HttpGet("Get/{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var chargeType = await _service.Get(id);
            if (chargeType == null)
            {
                return NotFound();
            }
            return Ok(chargeType);
        }

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var chargeTypes = await _service.GetAll();
            if (chargeTypes == null || !chargeTypes.Any())
            {
                return NotFound();
            }
            return Ok(chargeTypes);
        }

        [HttpGet("GetByClassification/{classificationId}")]
        public async Task<IActionResult> GetByClassification(int classificationId)
        {
            var chargeTypes = await _service.GetByClassificationId(classificationId);
            if (chargeTypes == null || !chargeTypes.Any())
            {
                return NotFound();
            }
            return Ok(chargeTypes);
        }

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<IActionResult> SaveUpdate(int userId, ChargeType chargeType)
        {
            var result = await _service.SaveUpdate(userId, chargeType);
            if (result == null)
            {
                return BadRequest();
            }
            return Ok(result);
        }

        #endregion

    }
}
