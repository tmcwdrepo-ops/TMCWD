using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Context;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;
using TMCWD.Utility.Generic;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/{requestId}/[controller]")]
    public class RecommendationController : Controller
    {

        private readonly IRecommendationService _recommendationService;

        public RecommendationController(IRecommendationService service)
        {
            _recommendationService = service;
        }

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<ActionResult> SaveUpdate(int userId, int requestId, [FromBody] Recommendation recommendation)
        {

            if (String.IsNullOrEmpty(recommendation.Details.Trim())) return BadRequest("Recommendation details is required");

            var updatedRecommendation = await _recommendationService.SaveUpdate(userId, requestId, recommendation);
            if (updatedRecommendation == null || updatedRecommendation.Id <= 0) return BadRequest("Problem(s) encountered while saving recommendation");

            return Ok(updatedRecommendation);
        }

        [HttpGet("Get/{id}")]
        public async Task<ActionResult> Get(int id)
        {
            var recommendation = await _recommendationService.Get(id);

            if (recommendation == null) return NotFound($"Recommendation with id {id} was not found");

            return Ok(recommendation);
        }

        [HttpGet("GetByRequestId")]
        public async Task<ActionResult> GetByRequestId(int requestId)
        {
            var recommendations = await _recommendationService.GetByRequestId(requestId);

            if (recommendations == null || !recommendations.Any()) return NotFound($"Recommendation(s) with for request {requestId} was not found.");

            return Ok(recommendations);
        }

    }
}
