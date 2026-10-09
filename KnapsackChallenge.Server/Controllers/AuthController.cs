using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KnapsackChallenge.Common.Constants;
using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Services.Auth;
using KnapsackChallenge.Server.Services.Auth;

namespace KnapsackChallenge.Server.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public sealed class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;
        private readonly IJwtTokenService _jwt;

        public AuthController(IAuthService auth, IJwtTokenService jwt)
        {
            _auth = auth;
            _jwt = jwt;
        }

        // POST /api/auth/login
        // - Sai user/pass  → 401 với thông báo chung (không tiết lộ user tồn tại).
        // - Tài khoản bị ban → 403 body { reason }.
        // - Thành công → { token, expiresAtUtc, userId, username, role }.
        [HttpPost("login")]
        [AllowAnonymous]
        public IActionResult Login([FromBody] LoginRequestDto? req)
        {
            if (req == null
                || string.IsNullOrWhiteSpace(req.Username)
                || string.IsNullOrEmpty(req.Password))
            {
                return Unauthorized(new { reason = Messages.AuthInvalidCredentials });
            }

            try
            {
                var user = _auth.Login(req.Username, req.Password);
                if (user == null)
                    return Unauthorized(new { reason = Messages.AuthInvalidCredentials });

                var (token, expires) = _jwt.CreateToken(user);
                return Ok(new LoginResponseDto
                {
                    Token = token,
                    ExpiresAtUtc = expires,
                    UserId = user.Id,
                    Username = user.Username ?? "",
                    Role = user.Role ?? "Player",
                });
            }
            catch (AccountBannedException ex)
            {
                // Không log mật khẩu ở bất kỳ đâu.
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { reason = ex.Message });
            }
        }
    }
}