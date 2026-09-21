using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TMCWD.Administration;

namespace TMCWD.Application.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationLoginTransaction _applicationLoginTransaction;
        private readonly IConfiguration _configuration;

        public AuthController(
            ApplicationLoginTransaction applicationLoginTransaction,
            IConfiguration configuration)
        {
            _applicationLoginTransaction = applicationLoginTransaction;
            _configuration = configuration;
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

        private string GenerateJwtToken(int userId, int role)
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
                expires: DateTime.Now.AddDays(30), // long-lived for field workers with spotty connectivity
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}