using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.ComponentModel.DataAnnotations;
using TMCWD.Administration;

namespace TMCWD.Application.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationLoginTransaction _applicationLoginTransaction;
        private readonly IConfiguration _configuration;
        private readonly UserTransaction _userTransaction;

        public AuthController(
            ApplicationLoginTransaction applicationLoginTransaction,
            IConfiguration configuration,
            UserTransaction userTransaction)
        {
            _applicationLoginTransaction = applicationLoginTransaction;
            _configuration = configuration;
            _userTransaction = userTransaction;
        }

        public class MobileLoginRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] MobileLoginRequest request)
        {
            _applicationLoginTransaction.Email = request.Email;
            _applicationLoginTransaction.Password = request.Password;

            var user = await _applicationLoginTransaction.Login();

            if (user == null || user.Id <= 0)
                return Unauthorized(new { message = "Invalid email or password" });

            var token = GenerateJwtToken(user.Id, user?.Role ?? 0);

            return Ok(new
            {
                token,
                userId = user.Id,
                role = user.Role,
                email = user.Email
            });
        }

        public class MeterReaderLoginRequest
        {
            [Required, StringLength(100)]
            public string Identifier { get; set; } = string.Empty;

            [Required, StringLength(256)]
            public string Password { get; set; } = string.Empty;
        }

        [HttpPost("meter-reader/login")]
        public async Task<IActionResult> MeterReaderLogin([FromBody] MeterReaderLoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Identifier)
                || request.Identifier.Length > 100
                || string.IsNullOrWhiteSpace(request.Password)
                || request.Password.Length > 256)
                return BadRequest(new { message = "Login identifier and password are required." });

            var identifier = request.Identifier.Trim();
            int? expectedUserId = null;
            if (int.TryParse(identifier, out var userId))
            {
                if (userId <= 0)
                    return Unauthorized(new { message = "Invalid login or password." });
                var account = await _userTransaction.Get(userId);
                if (account == null || string.IsNullOrWhiteSpace(account.Email))
                    return Unauthorized(new { message = "Invalid login or password." });
                identifier = account.Email;
                expectedUserId = userId;
            }

            _applicationLoginTransaction.Email = identifier;
            _applicationLoginTransaction.Password = request.Password;
            var user = await _applicationLoginTransaction.Login();
            if (user.Id <= 0 || (expectedUserId.HasValue && user.Id != expectedUserId.Value))
                return Unauthorized(new { message = "Invalid login or password." });
            if (user.Role != (int)UserRole.MeterReader || user.IsActive != true)
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "An active meter-reader account is required." });

            // Match JWT's whole-second expiry so the device cannot outlive it.
            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(
                DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds()).UtcDateTime;
            var token = GenerateJwtToken(user.Id, user.Role.Value, expiresAt);
            return Ok(new { token, userId = user.Id, role = user.Role, email = user.Email,
                name = user.Name, expiresAt });
        }

        private string GenerateJwtToken(int userId, int role, DateTime? expiresAt = null)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role.ToString())
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: expiresAt ?? DateTime.UtcNow.AddDays(30), // field workers with spotty connectivity
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
