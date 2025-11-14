using HackathonIService;
using HackathonModels.Authentication;
using HackathonModels.Registeruser;
using HackathonService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace HackathonApiProject.Controllers
{
    [AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;

        public AuthController(IAuthService auth) {
            _auth = auth;
        }

        [HttpPost("signup")]
        public async Task<IActionResult> Register([FromBody] RegisterUserRequest req)
        {
            try
            {
                await _auth.RegisterUserAsync(req);

                return Ok(new
                {
                    message = "User registered successfully"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (string.IsNullOrEmpty(req.Username) || string.IsNullOrEmpty(req.Password))
                return BadRequest("Username and password required");

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var tokens = await _auth.LoginAsync(req, ip);
            if (tokens == null) return Unauthorized("Invalid credentials");
            return Ok(tokens);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken)) return BadRequest("refreshToken required");
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var tokens = await _auth.RefreshTokenAsync(refreshToken, ip);
            if (tokens == null) return Unauthorized("Invalid or expired refresh token");
            return Ok(tokens);
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken)) return BadRequest("refreshToken required");
            await _auth.RevokeRefreshTokenAsync(refreshToken);
            return Ok(new { message = "Logged out (refresh token revoked)" });
        }

        // test protected endpoint
        [Authorize(Roles = "Admin")]
        [HttpGet("admin-only")]
        public IActionResult AdminOnly() => Ok("Hello Admin!");
    }
}

