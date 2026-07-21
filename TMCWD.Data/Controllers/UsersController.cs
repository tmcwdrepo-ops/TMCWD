using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Text;
using TMCWD.Data.Entities;
using TMCWD.Data.Services;
using TMCWD.Utility.Encryption;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : Controller
    {

        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpPost("SaveUpdate/{userId}")]
        public async Task<ActionResult> SaveUpdate(int userId, [FromBody] User user)
        {
            StringBuilder sb = new();

            if (String.IsNullOrEmpty(user.Name.Trim())) sb.AppendLine("Name is required");
            if (String.IsNullOrEmpty(user.Email.Trim())) sb.AppendLine("Email is required");
            if (String.IsNullOrEmpty(user.Password.Trim())) sb.AppendLine("Password is required");

            if (!String.IsNullOrEmpty(sb.ToString().Trim())) return BadRequest(sb.ToString());

            if(user.Id > 0)
            {
                user.UpdatedBy = userId;
            }
            else
            {

                var existingUser = await _userService.GetByEmail(StringEncyption.Encrypt(user.Email));

                if (existingUser != null)
                    return BadRequest($"User with email {user.Email} already exists.");

                user.CreatedBy = userId;
                user.DateCreated = DateTime.Now;
                user.DateVerified = DateTime.Now;
            }
            user.DateUpdated = DateTime.Now;


            var updatedUser = await _userService.SaveUpdate(userId, user);

            return Ok(updatedUser);
        }

        [HttpGet("Get/{id}")]
        public async Task<ActionResult> Get(int id)
        {

            if (id <= 0) return BadRequest("To get user, id must be supplied");

            var user = await _userService.Get(id);
            if (user == null) return NotFound($"User with id {id} was not found");
            return Ok(user);
        }

        [HttpGet("GetByName/{name}")]
        public async Task<ActionResult> GetByName(string name)
        {

            if (String.IsNullOrEmpty(name.Trim())) return BadRequest("To get user, name must be supplied");

            var user = await _userService.GetByName(name);
            if (user == null) return NotFound($"User with name {name} was not found");
            return Ok(user);
        }

        [HttpGet("GetByEmail/{email}")]
        public async Task<ActionResult> GetByEmail(string email)
        {

            if (String.IsNullOrEmpty(email.Trim())) return BadRequest("To get user, email must be supplied");

            var user = await _userService.GetByEmail(email);

            if (user == null) return NotFound($"User with email {email} was not found.");

            return Ok(user);
        }

        [HttpGet("GetUsers")]
        public async Task<ActionResult> GetUsers()
        {
            var users = await _userService.GetUsers();
            if (users == null || !users.Any()) return NotFound();
            return Ok(users);
        }

        [HttpPut("ChangePassword/{id}/{userId}/{newPassword}")]
        public async Task<ActionResult> ChangePassword(int id, int userId, string newPassword)
        {
            StringBuilder sb = new();
            if (id <= 0) sb.AppendLine("To change password, user id must be supplied");
            if (string.IsNullOrEmpty(newPassword.Trim())) sb.AppendLine("To change password, new password must be supplied");
            if (!String.IsNullOrEmpty(sb.ToString().Trim())) return BadRequest(sb.ToString());

            var existingUser = await _userService.Get(id);
            if (existingUser == null) return NotFound($"User with id {id} was not found");
            existingUser.Password = newPassword;

            var updatedUser = await _userService.SaveUpdate(userId, existingUser);

            return Ok(updatedUser.Id > 0);
        }

        [HttpGet("SearchUser/{searchString}")]
        public async Task<ActionResult> SearchUser(string searchString)
        {
            var users = await _userService.SearchUser(searchString);

            if(users == null  || !users.Any()) return NotFound($"User(s) with value {searchString} was not found.");

            return Ok(users);
        }

        [HttpGet("GetUsersByRole/{role}")]
        public async Task<IActionResult> GetUsersByRole(int role)
        {
            var users = await _userService.GetUsersByRole(role);
            if (users == null || !users.Any()) return NotFound();
            return Ok(users);
        }

    }
}