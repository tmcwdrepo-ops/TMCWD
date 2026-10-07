using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Services;
using TMCWD.Model.Billing;

namespace TMCWD.Data.Controllers;

/// <summary>
/// Provides read access to the active BIR invoice configuration.
/// Configuration access and changes require SuperAdmin authorization.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = nameof(UserRole.SuperAdmin))]
public class BirInvoiceConfigController(IBirInvoiceConfigService configService) : ControllerBase
{
    [HttpPost("active")]
    public async Task<ActionResult<BirInvoiceConfigDto>> SaveActive(BirInvoiceConfigDto request)
    {
        if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId) || userId <= 0)
            return Challenge();
        if (string.IsNullOrWhiteSpace(request.PTUNumber)) return BadRequest();
        try
        {
            var config = await configService.SaveUpdate(userId, new TMCWD.Data.Entities.BirInvoiceConfig {
                Id = request.Id, PTUNumber = request.PTUNumber, PermitDateIssued = request.PermitDateIssued,
                SeriesFrom = request.SeriesFrom, SeriesTo = request.SeriesTo, InvoicePrefix = request.InvoicePrefix,
                SeriesWidth = request.SeriesWidth, EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo,
                IsActive = true
            });
            return Ok(ToDto(config));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
        catch (InvalidOperationException error) { return Conflict(new { message = error.Message }); }
    }

    /// <summary>
    /// Returns the currently active BIR configuration, or 204 No Content
    /// when no active configuration has been set up yet.
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<BirInvoiceConfigDto>> GetActive()
    {
        var config = await configService.GetActive();
        if (config == null) return NoContent();
        return Ok(ToDto(config));
    }

    /// <summary>
    /// Returns all BIR configurations, active and inactive, ordered by
    /// PermitDateIssued descending. Used for administration screens.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<BirInvoiceConfigDto>>> GetAll()
    {
        var configs = await configService.GetAll();
        return Ok(configs.Select(ToDto).ToList());
    }

    private static BirInvoiceConfigDto ToDto(TMCWD.Data.Entities.BirInvoiceConfig c) =>
        new(c.Id, c.PTUNumber, c.PermitDateIssued,
            c.SeriesFrom, c.SeriesTo, c.EffectiveFrom, c.EffectiveTo, c.InvoicePrefix, c.SeriesWidth);
}
