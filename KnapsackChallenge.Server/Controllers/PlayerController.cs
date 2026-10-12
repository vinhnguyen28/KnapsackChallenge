using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Services.Player;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnapsackChallenge.Server.Controllers
{
    // REST endpoints cho phiên chơi của Player.
    // userId LUÔN đọc từ claim "sub" qua TryGetUserId — không nhận từ client.
    [ApiController]
    [Route("api/player")]
    [Authorize]
    public sealed class PlayerController : ControllerBase
    {
        private readonly IPlayerSessionService _session;
        private readonly IRankService _rank;

        public PlayerController(IPlayerSessionService session, IRankService rank)
        {
            _session = session;
            _rank = rank;
        }

        // POST /api/player/heartbeat
        // Cập nhật LastSeenAt + hồi tim. Trả trạng thái tim hiện tại.
        [HttpPost("heartbeat")]
        public IActionResult Heartbeat()
        {
            if (!this.TryGetUserId(out int userId)) return Unauthorized();
            return Ok(_session.Heartbeat(userId));
        }

        // GET /api/player/hearts
        // Chỉ đọc trạng thái tim (không đụng LastSeenAt).
        [HttpGet("hearts")]
        public IActionResult Hearts()
        {
            if (!this.TryGetUserId(out int userId)) return Unauthorized();
            return Ok(_session.GetHeartStatus(userId));
        }

        // POST /api/player/offline
        // Đặt LastSeenAt = NULL khi client đóng app / đăng xuất.
        [HttpPost("offline")]
        public IActionResult Offline()
        {
            if (!this.TryGetUserId(out int userId)) return Unauthorized();
            _session.GoOffline(userId);
            return NoContent();
        }

        // GET /api/player/status
        // Cho heartbeat: client tự logout khi Banned = true.
        [HttpGet("status")]
        public IActionResult Status()
        {
            if (!this.TryGetUserId(out int userId)) return Unauthorized();

            var (banned, reason) = _session.CheckStatus(userId);
            return Ok(new PlayerBanStatusDto
            {
                Banned = banned,
                Reason = reason,
            });
        }

        // GET /api/player/rank
        // Trạng thái Rank/EXP hiện tại của người chơi.
        [HttpGet("rank")]
        public IActionResult Rank()
        {
            if (!this.TryGetUserId(out int userId)) return Unauthorized();
            return Ok(_rank.GetStatus(userId));
        }
    }
}