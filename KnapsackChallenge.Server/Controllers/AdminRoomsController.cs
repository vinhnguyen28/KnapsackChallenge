using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KnapsackChallenge.Core.Services.Player.Multiplayer;

namespace KnapsackChallenge.Server.Controllers
{
    public sealed class AdminKickRequest
    {
        public int UserId { get; set; }
        public string Reason { get; set; } = "";
    }

    [ApiController]
    [Route("api/admin/rooms")]
    [Authorize(Roles = "Admin")]
    public sealed class AdminRoomsController : ControllerBase
    {
        private readonly IGameRoomService _rooms;

        public AdminRoomsController(IGameRoomService rooms)
        {
            _rooms = rooms;
        }

        // GET /api/admin/rooms
        [HttpGet]
        public async Task<IActionResult> List()
            => Ok(await _rooms.ListRoomsAsync());

        // GET /api/admin/rooms/{code}
        [HttpGet("{code}")]
        public async Task<IActionResult> Get(string code)
        {
            // (4) Chuẩn hoá mã phòng trước khi tra cứu.
            code = (code ?? "").Trim().ToUpperInvariant();

            var result = await _rooms.GetRoomStateByCodeAsync(code);
            if (!result.Success)
                return NotFound(new { code = result.ErrorCode, message = result.Message });
            return Ok(result.Data);
        }

        // POST /api/admin/rooms/{code}/kick
        [HttpPost("{code}/kick")]
        public async Task<IActionResult> Kick(string code, [FromBody] AdminKickRequest? req)
        {
            if (req == null || req.UserId <= 0)
                return BadRequest(new { message = "userId không hợp lệ." });

            // (4) Chuẩn hoá mã phòng.
            code = (code ?? "").Trim().ToUpperInvariant();

            var result = await _rooms.KickAsync(code, req.UserId, req.Reason ?? "");
            if (!result.Success)
                return BadRequest(new { code = result.ErrorCode, message = result.Message });

            return Ok(new { success = true, message = "Đã kick người chơi khỏi phòng." });
        }
    }
}