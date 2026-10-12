using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Services.Player;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnapsackChallenge.Server.Controllers
{
    // REST endpoints cho chế độ Solo.
    // userId LUÔN đọc từ claim "sub" qua TryGetUserId — KHÔNG nhận từ client.
    [ApiController]
    [Route("api/solo")]
    [Authorize]
    public sealed class SoloController : ControllerBase
    {
        // Biên chống DoS từ client gửi list khổng lồ / thời gian vô lý.
        private const int MaxSubmitItemIds = 500;
        private const int MaxTimeSpentSeconds = 86400; // 24h
        private const int MaxHistoryLimit = 200;
        private const int MaxLeaderboardTopN = 100;

        private readonly ISoloGameService _solo;

        public SoloController(ISoloGameService solo)
        {
            _solo = solo;
        }

        // GET /api/solo/status
        [HttpGet("status")]
        public IActionResult Status()
        {
            var (enabled, timeLimit) = _solo.GetSoloModeStatus();
            return Ok(new SoloModeStatusDto
            {
                IsEnabled = enabled,
                TimeLimitSeconds = timeLimit,
            });
        }

        // GET /api/solo/sets
        [HttpGet("sets")]
        public IActionResult Sets()
        {
            return Ok(_solo.GetAvailableSets());
        }

        // POST /api/solo/start
        // - Lỗi nghiệp vụ (hết tim, mode tắt, bộ đề hỏng) → HTTP 200 + Success=false.
        // - Lỗi validate (SetId <= 0) → HTTP 400 { message }.
        [HttpPost("start")]
        public IActionResult Start([FromBody] StartGameRequestDto? req)
        {
            if (!this.TryGetUserId(out int userId)) return Unauthorized();
            if (req == null || req.SetId <= 0)
                return BadRequest(new { message = "SetId không hợp lệ." });

            var (success, message, data, heart) = _solo.StartGame(userId, req.SetId);

            return Ok(new StartGameResponseDto
            {
                Success = success,
                Message = message,
                Data = data,
                HeartStatus = heart,
            });
        }

        // POST /api/solo/submit
        [HttpPost("submit")]
        public IActionResult Submit([FromBody] SubmitSoloRequestDto? req)
        {
            if (!this.TryGetUserId(out int userId)) return Unauthorized();
            if (req == null || req.SetId <= 0)
                return BadRequest(new { message = "SetId không hợp lệ." });
            if (req.SelectedItemIds == null)
                return BadRequest(new { message = "Danh sách vật phẩm không hợp lệ." });
            if (req.SelectedItemIds.Count > MaxSubmitItemIds)
                return BadRequest(new { message = $"Quá nhiều vật phẩm (tối đa {MaxSubmitItemIds})." });

            // Ép thời gian về biên hợp lệ — không tin số client gửi.
            int timeSpent = req.TimeSpentSeconds;
            if (timeSpent < 0) timeSpent = 0;
            if (timeSpent > MaxTimeSpentSeconds) timeSpent = MaxTimeSpentSeconds;

            var (success, message, result) = _solo.Submit(
                userId, req.SetId, req.SelectedItemIds, timeSpent, req.IsTimeout);

            return Ok(new ApiResult<SoloResultDto>
            {
                Success = success,
                Message = message,
                Data = result,
            });
        }

        // GET /api/solo/history?limit=20
        [HttpGet("history")]
        public IActionResult History([FromQuery] int limit = 20)
        {
            if (!this.TryGetUserId(out int userId)) return Unauthorized();

            if (limit < 1) limit = 1;
            if (limit > MaxHistoryLimit) limit = MaxHistoryLimit;

            return Ok(_solo.GetHistory(userId, limit));
        }

        // GET /api/solo/leaderboard?setId=&topN=20
        // setId null = tất cả bộ đề.
        [HttpGet("leaderboard")]
        public IActionResult Leaderboard([FromQuery] int? setId, [FromQuery] int topN = 20)
        {
            if (setId.HasValue && setId.Value <= 0)
                return BadRequest(new { message = "setId không hợp lệ." });

            if (topN < 1) topN = 1;
            if (topN > MaxLeaderboardTopN) topN = MaxLeaderboardTopN;

            return Ok(_solo.GetLeaderboard(setId, topN));
        }
    }
}