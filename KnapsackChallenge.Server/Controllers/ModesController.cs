using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnapsackChallenge.Server.Controllers
{
    // Endpoint công khai cho Player đọc trạng thái các chế độ chơi.
    // KHÔNG nhận userId từ client — nhưng endpoint này không cần userId.
    [ApiController]
    [Route("api/modes")]
    [Authorize]
    public sealed class ModesController : ControllerBase
    {
        private readonly IGameModeService _gameModes;

        public ModesController(IGameModeService gameModes)
        {
            _gameModes = gameModes;
        }

        // GET /api/modes
        // Trả danh sách chế độ chơi (đã lược bỏ field Admin-only).
        [HttpGet]
        public IActionResult List()
        {
            var list = _gameModes.GetAll()
                .Select(m => new PlayableModeDto
                {
                    ModeKey = m.ModeKey,
                    IsEnabled = m.IsEnabled,
                    TimeLimitSeconds = m.TimeLimitSeconds,
                    MaxPlayers = m.MaxPlayers,
                })
                .ToList();

            return Ok(list);
        }
    }
}