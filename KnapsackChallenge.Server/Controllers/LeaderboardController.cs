using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Services.Player;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnapsackChallenge.Server.Controllers
{
    // REST endpoints cho bảng xếp hạng mới (v9): theo EXP và theo bộ đề.
    // userId LUÔN đọc từ claim "sub" qua TryGetUserId — KHÔNG nhận từ client.
    [ApiController]
    [Route("api/leaderboard")]
    [Authorize]
    public sealed class LeaderboardController : ControllerBase
    {
        private const int MinTopN = 1;
        private const int MaxTopN = 200;

        private readonly ILeaderboardService _leaderboard;

        public LeaderboardController(ILeaderboardService leaderboard)
        {
            _leaderboard = leaderboard;
        }

        // GET /api/leaderboard/exp?topN=100
        [HttpGet("exp")]
        public IActionResult Exp([FromQuery] int topN = 100)
        {
            int n = ClampTopN(topN);
            return Ok(_leaderboard.GetByExp(n));
        }

        // GET /api/leaderboard/exp/me
        // Trả 204 NoContent nếu người chơi chưa có EXP / không tồn tại / bị ban.
        [HttpGet("exp/me")]
        public IActionResult MyExp()
        {
            if (!this.TryGetUserId(out int userId)) return Unauthorized();

            var entry = _leaderboard.GetMyExpRank(userId);
            if (entry == null) return NoContent();
            return Ok(entry);
        }

        // GET /api/leaderboard/sets?mode=&difficulty=&setId=&sinceUtc=&topN=100
        [HttpGet("sets")]
        public IActionResult Sets(
            [FromQuery] string? mode,
            [FromQuery] string? difficulty,
            [FromQuery] int? setId,
            [FromQuery] DateTime? sinceUtc,
            [FromQuery] int topN = 100)
        {
            var (query, error) = BuildQuery(mode, difficulty, setId, sinceUtc, topN, includeTopN: true);
            if (error != null)
                return BadRequest(new { message = error });

            return Ok(_leaderboard.GetBySet(query!));
        }

        // GET /api/leaderboard/sets/me?mode=&difficulty=&setId=&sinceUtc=
        // Trả 204 NoContent nếu người chơi chưa có thứ hạng trong phạm vi lọc.
        [HttpGet("sets/me")]
        public IActionResult MySet(
            [FromQuery] string? mode,
            [FromQuery] string? difficulty,
            [FromQuery] int? setId,
            [FromQuery] DateTime? sinceUtc)
        {
            if (!this.TryGetUserId(out int userId)) return Unauthorized();

            var (query, error) = BuildQuery(mode, difficulty, setId, sinceUtc, topN: 100, includeTopN: false);
            if (error != null)
                return BadRequest(new { message = error });

            var entry = _leaderboard.GetMySetRank(userId, query!);
            if (entry == null) return NoContent();
            return Ok(entry);
        }

        // =========================================================
        // Helpers
        // =========================================================

        private static int ClampTopN(int topN)
        {
            if (topN < MinTopN) return MinTopN;
            if (topN > MaxTopN) return MaxTopN;
            return topN;
        }

        // Trả (query, null) khi hợp lệ; (null, errorMessage) khi sai.
        // Controller KHÔNG gọi LeaderboardService.Normalize (internal), tự chuẩn hoá.
        private static (LeaderboardQuery? Query, string? Error) BuildQuery(
            string? mode,
            string? difficulty,
            int? setId,
            DateTime? sinceUtc,
            int topN,
            bool includeTopN)
        {
            string? normalizedMode = null;
            if (!string.IsNullOrWhiteSpace(mode))
            {
                var m = mode.Trim();
                if (string.Equals(m, "Solo", StringComparison.OrdinalIgnoreCase))
                    normalizedMode = "Solo";
                else if (string.Equals(m, "Multiplayer", StringComparison.OrdinalIgnoreCase))
                    normalizedMode = "Multiplayer";
                else
                    return (null, "Chế độ không hợp lệ (chỉ nhận Solo hoặc Multiplayer).");
            }

            string? normalizedDifficulty = null;
            if (!string.IsNullOrWhiteSpace(difficulty))
            {
                var d = difficulty.Trim();
                if (string.Equals(d, "Easy", StringComparison.OrdinalIgnoreCase))
                    normalizedDifficulty = "Easy";
                else if (string.Equals(d, "Medium", StringComparison.OrdinalIgnoreCase))
                    normalizedDifficulty = "Medium";
                else if (string.Equals(d, "Hard", StringComparison.OrdinalIgnoreCase))
                    normalizedDifficulty = "Hard";
                else
                    return (null, "Độ khó không hợp lệ (chỉ nhận Easy, Medium hoặc Hard).");
            }

            if (setId.HasValue && setId.Value <= 0)
                return (null, "setId phải lớn hơn 0.");

            DateTime? normalizedSince = null;
            if (sinceUtc.HasValue)
            {
                var value = sinceUtc.Value.Kind == DateTimeKind.Utc
                    ? sinceUtc.Value
                    : sinceUtc.Value.ToUniversalTime();

                var minUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var maxUtc = DateTime.UtcNow.AddMinutes(5);

                if (value < minUtc || value > maxUtc)
                    return (null, "sinceUtc phải nằm trong khoảng từ 2020-01-01 đến hiện tại.");

                normalizedSince = value;
            }

            var query = new LeaderboardQuery
            {
                Mode = normalizedMode,
                Difficulty = normalizedDifficulty,
                SetId = setId,
                SinceUtc = normalizedSince,
                TopN = includeTopN ? ClampTopN(topN) : 100,
            };

            return (query, null);
        }
    }
}