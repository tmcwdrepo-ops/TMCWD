using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using TMCWD.Data.Services;
using TMCWD.Model.Billing;

namespace TMCWD.Data.Controllers;

[ApiController, Route("api/DisconnectionMonitoring")]
public sealed class DisconnectionMonitoringController(DisconnectionMonitoringService service, ILogger<DisconnectionMonitoringController> logger) : ControllerBase
{
    [HttpGet, Authorize(Policy = "BillingOperations")]
    public Task<IActionResult> List(string? q, int? zone, int? book) => Execute(async () =>
        Ok(await service.List(q, zone, book, DateOnly.FromDateTime(LatePaymentPenalty.Today))));

    [HttpGet("{accountNumber}"), Authorize(Policy = "BillingOperations")]
    public Task<IActionResult> Get(string accountNumber) => Execute(async () => {
        var result = await service.Get(accountNumber, DateOnly.FromDateTime(LatePaymentPenalty.Today));
        return result == null ? NotFound(new { message = "Account not found." }) : Ok(result);
    });

    [HttpPost("{accountNumber}/disconnect"), Authorize(Policy = "ConfirmDisconnection")]
    public Task<IActionResult> Disconnect(string accountNumber, ServiceConfirmationRequest request) => Confirm(accountNumber, request, false);

    [HttpPost("{accountNumber}/reconnect"), Authorize(Policy = "ConfirmReconnection")]
    public Task<IActionResult> Reconnect(string accountNumber, ServiceConfirmationRequest request) => Confirm(accountNumber, request, true);

    Task<IActionResult> Confirm(string accountNumber, ServiceConfirmationRequest request, bool reconnect) => Execute(async () => {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actor) || actor <= 0) return Unauthorized();
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now.AddHours(8));
        var result = reconnect ? await service.ConfirmReconnection(accountNumber, request.OperationId, actor, now, today)
            : await service.ConfirmDisconnection(accountNumber, request.OperationId, actor, now, today);
        return Ok(new { eventId = result.Id, result.ActualDisconnectionDateUtc, result.ActualReconnectionDateUtc,
            result.DisconnectedBy, result.ReconnectedBy, result.ReconnectionFeeOtherChargeId });
    });

    async Task<IActionResult> Execute(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (ArgumentException) { return BadRequest(new { message = "Invalid account, filter or confirmation identity." }); }
        catch (KeyNotFoundException) { return NotFound(new { message = "Account not found." }); }
        catch (InvalidOperationException) { return Conflict(new { message = "The account requires reconciliation or does not qualify for this operation." }); }
        catch (DbUpdateException e) when (e.InnerException is MySqlException { Number: 1062 }) {
            return Conflict(new { message = "Confirmation identity conflicts with an existing event." });
        }
        catch (MySqlException e) when (e.Number == 1146) {
            return StatusCode(503, new { message = "Disconnection monitoring requires its reviewed database migration." });
        }
        catch (Exception e) {
            logger.LogError("Disconnection monitoring failed; category {Category}", e.GetType().Name);
            return StatusCode(500, new { message = "Unable to load or confirm service state. Please retry." });
        }
    }
}
