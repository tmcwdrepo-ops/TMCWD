using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;
using TMCWD.Model.Administrator;
using TMCWD.Model.Billing;

namespace TMCWD.Data.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MeterMaintenanceRateController(IMeterMaintenanceRateService service) : ControllerBase
{
    [HttpGet("by-billing-date/{billingDate}")]
    [Authorize(Policy = "BillingOperations")]
    public async Task<ActionResult<MeterMaintenanceRateDto>> GetForBillingDate(DateOnly billingDate)
    {
        try
        {
            return Ok(ToDto(await service.GetForBillingDate(billingDate)));
        }
        catch (KeyNotFoundException error)
        {
            return NotFound(new { message = error.Message });
        }
        catch (InvalidOperationException error)
        {
            return Conflict(new { message = error.Message });
        }
    }

    [HttpGet]
    [Authorize(Policy = nameof(UserRole.SuperAdmin))]
    public async Task<ActionResult<List<MeterMaintenanceRateDto>>> GetAll() =>
        Ok((await service.GetAll()).Select(ToDto).ToList());

    [HttpPost]
    [Authorize(Policy = nameof(UserRole.SuperAdmin))]
    public async Task<ActionResult<MeterMaintenanceRateDto>> SaveUpdate(MeterMaintenanceRateRequest request)
    {
        if (!long.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId)
            || userId <= 0)
            return Challenge();

        try
        {
            var saved = await service.SaveUpdate(userId, new MeterMaintenanceRate
            {
                Id = request.Id,
                Amount = request.Amount,
                EffectiveFrom = request.EffectiveFrom,
                EffectiveTo = request.EffectiveTo
            });
            return Ok(ToDto(saved));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException error)
        {
            return NotFound(new { message = error.Message });
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { message = error.Message });
        }
        catch (InvalidOperationException error)
        {
            return Conflict(new { message = error.Message });
        }
    }

    private static MeterMaintenanceRateDto ToDto(MeterMaintenanceRate rate) =>
        new(rate.Id, rate.Amount, rate.EffectiveFrom, rate.EffectiveTo,
            rate.CreatedBy, rate.DateCreated, rate.UpdatedBy, rate.DateUpdated);
}
