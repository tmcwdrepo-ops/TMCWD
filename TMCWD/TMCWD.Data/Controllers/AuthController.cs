using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TMCWD.Data.Services;
using TMCWD.Utility.Encryption;

namespace TMCWD.Data.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IOptionsMonitor<JwtBearerOptions> _bearerOptions;

        public AuthController(IUserService userService, IOptionsMonitor<JwtBearerOptions> bearerOptions)
        {
            _userService = userService;
            _bearerOptions = bearerOptions;
        }

        [HttpPost("meter-reader/login")]
        public async Task<IActionResult> MeterReaderLogin(
            [FromBody] MeterReaderLoginRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Identifier) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new
                {
                    message = "Email and password are required."
                });
            }

            var email = request.Identifier.Trim();

            // Users are stored with their normal/plain email address.
            var user = await _userService.GetByEmail(email);

            // Do not reveal whether the account exists.
            if (user == null)
            {
                return Unauthorized(new
                {
                    message = "Invalid login or password."
                });
            }

            // Existing TMCWD password format uses MD5 through
            // StringEncyption.Encrypt().
            var encryptedPassword =
                StringEncyption.Encrypt(request.Password);

            if (!string.Equals(
                    user.Password,
                    encryptedPassword,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized(new
                {
                    message = "Invalid login or password."
                });
            }

            // Mobile application is only for meter readers.
            if (user.Role != (int)UserRole.MeterReader)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "This account is not a meter-reader account."
                });
            }

            if (user.IsActive != true)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        message = "This meter-reader account is inactive."
                    });
                }

                if (user.IsVerified != true)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        message = "This meter-reader account is not verified."
                    });
                }

            // Preserve the Data login's 12-hour lifetime and match JWT's second precision.
            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(
                DateTimeOffset.UtcNow.AddHours(12).ToUnixTimeSeconds()).UtcDateTime;
            var validation = _bearerOptions.Get(JwtBearerDefaults.AuthenticationScheme).TokenValidationParameters;
            var handler = new JwtSecurityTokenHandler();
            var token = handler.WriteToken(handler.CreateToken(new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
                    new Claim(ClaimTypes.Role, ((int)UserRole.MeterReader).ToString(CultureInfo.InvariantCulture))
                ]),
                Issuer = validation.ValidIssuer,
                Audience = validation.ValidAudience,
                Expires = expiresAt,
                SigningCredentials = new SigningCredentials(validation.IssuerSigningKey, SecurityAlgorithms.HmacSha256)
            }));

            return Ok(new
            {
                token,
                userId = (int)user.Id,
                role = (int)UserRole.MeterReader,
                name = user.Name,
                email = user.Email,
                expiresAt
            });
        }
    }

    public class MeterReaderLoginRequest
    {
        public string Identifier { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
