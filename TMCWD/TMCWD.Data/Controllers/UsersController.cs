using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TMCWD.Data.Services;
using TMCWD.Model.Administrator;
using TMCWD.Utility.Encryption;
using User = TMCWD.Data.Entities.User;

namespace TMCWD.Data.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController(IUserService users) : ControllerBase
{
    private int ActorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id > 0 ? id : 0;

    [HttpPost("SaveUpdate/{userId}")]
    [Authorize(Policy = nameof(UserRole.SuperAdmin))]
    public async Task<IActionResult> SaveUpdate(int userId, UserWriteRequest request)
    {
        userId = ActorId;
        if (userId == 0) return Unauthorized();
        var actor = await users.Get(userId);
        if (actor?.Role != (int)UserRole.SuperAdmin || actor.IsActive != true || actor.IsVerified != true)
            return Forbid();
        var existing = request.Id > 0 ? await users.Get(request.Id) : null;
        if (request.Id > 0 && existing == null) return NotFound();
        // Peer administrator changes and verification require a separately approved account-security workflow.
        if (existing?.Role == (int)UserRole.SuperAdmin && existing.Id != userId) return Forbid();
        if (existing?.Id == userId && ((request.Role.HasValue && request.Role != existing.Role)
            || (request.IsActive.HasValue && request.IsActive != existing.IsActive))) return Forbid();
        if (existing != null && !string.IsNullOrEmpty(request.Password))
            return BadRequest("Use the password change command.");
        if (existing == null && (!request.Role.HasValue || string.IsNullOrWhiteSpace(request.Password)))
            return BadRequest("A role and initial password are required.");
        var duplicate = await users.GetByEmail(request.Email.Trim());
        if (duplicate != null && duplicate.Id != request.Id) return Conflict("Email is already registered.");
        var saved = existing ?? new User { IsVerified = false, DateVerified = null };
        saved.Name = request.Name.Trim();
        saved.Email = request.Email.Trim();
        saved.Role = request.Role ?? saved.Role;
        saved.IsActive = request.IsActive ?? saved.IsActive ?? false;
        if (existing == null) saved.Password = StringEncyption.Encrypt(request.Password!);
        saved.UpdatedBy = userId;
        saved.DateUpdated = DateTime.Now;
        return Ok(Profile(await users.SaveUpdate(userId, saved)));
    }

    [HttpPost("password")]
    public async Task<IActionResult> ChangePassword(PasswordChangeRequest request)
    {
        var actorId = ActorId;
        if (actorId == 0) return Unauthorized();
        if (Request.Query.Count != 0) return BadRequest("Password changes accept a body and authenticated self identity only.");
        var account = await users.Get(actorId);
        if (account == null || account.IsActive != true || account.IsVerified != true) return Forbid();
        if (account.Password != StringEncyption.Encrypt(request.CurrentPassword)) return Forbid();
        if (request.NewPassword != request.ConfirmPassword) return BadRequest("Password confirmation failed.");
        account.Password = StringEncyption.Encrypt(request.NewPassword);
        await users.SaveUpdate(actorId, account);
        return NoContent();
    }

    [HttpGet("Get/{id}")]
    [Authorize(Policy = "BillingOperations")]
    public async Task<IActionResult> Get(int id) => Result(await users.Get(id));
    [HttpGet("GetByName/{name}")]
    [Authorize(Policy = nameof(UserRole.SuperAdmin))]
    public async Task<IActionResult> GetByName(string name) => Result(await users.GetByName(name));
    [HttpGet("GetByEmail/{email}")]
    [Authorize(Policy = nameof(UserRole.SuperAdmin))]
    public async Task<IActionResult> GetByEmail(string email) => Result(await users.GetByEmail(email));
    [HttpGet("GetUsers")]
    [Authorize(Policy = "BillingOperations")]
    public async Task<IActionResult> GetUsers() => Ok((await users.GetUsers() ?? []).Select(ReadProfile));
    [HttpGet("SearchUser/{searchString}")]
    [Authorize(Policy = nameof(UserRole.SuperAdmin))]
    public async Task<IActionResult> SearchUser(string searchString) => Ok((await users.SearchUser(searchString) ?? []).Select(ReadProfile));
    [HttpGet("GetUsersByRole/{role}")]
    [Authorize(Policy = "BillingOperations")]
    public async Task<IActionResult> GetUsersByRole(int role)
    {
        if (role != (int)UserRole.MeterReader && !User.IsInRole(((int)UserRole.SuperAdmin).ToString())) return Forbid();
        return Ok((await users.GetUsersByRole(role)).Select(ReadProfile));
    }
    [HttpGet("GetUsersById")]
    [Authorize(Policy = "BillingOperations")]
    public async Task<IActionResult> GetUsersById([FromQuery] int[] ids) => Ok((await users.GetUsersByIds(ids)).Select(ReadProfile));

    private object ReadProfile(User user) => User.IsInRole(((int)UserRole.SuperAdmin).ToString())
        ? Profile(user) : new { user.Id, user.Name };
    private IActionResult Result(User? user) => user == null ? NotFound() : Ok(ReadProfile(user));
    internal static UserProfileDto Profile(User user) => new(user.Id, user.Name, user.Email, user.Role,
        user.IsActive, user.IsVerified, user.DateCreated, user.DateUpdated);
}
