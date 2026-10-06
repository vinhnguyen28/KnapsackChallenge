using KnapsackChallenge.Common.DTOs;

namespace KnapsackChallenge.Core.Services.Player
{
    // Nghiệp vụ chơi Solo (server-side, KHÔNG tin số liệu từ UI).
    public interface ISoloGameService
    {
        // v4: trạng thái chế độ Solo để UI hiển thị banner.
        (bool IsEnabled, int TimeLimitSeconds) GetSoloModeStatus();

        // Bộ đề có ít nhất 1 vật phẩm.
        List<SoloGameSetDto> GetAvailableSets();

        // v4: trả tuple để báo được lý do từ chối (chế độ tắt, ...).
        (bool Success, string Message, SoloGameDataDto? Data) StartGame(int setId);

        // Nộp bài.
        // - isTimeout = true: cho phép selection rỗng; nếu vượt sức chứa thì tự bỏ hết.
        // - Server tự tính lại toàn bộ điểm/khối lượng.
        // - v4: từ chối nếu chế độ Solo đang tắt; từ chối nếu timeSpent > TimeLimit + biên 5s.
        (bool Success, string Message, SoloResultDto? Result) Submit(
            int userId,
            int setId,
            IEnumerable<int> selectedItemIds,
            int timeSpentSeconds,
            bool isTimeout = false);

        List<GameHistoryDto> GetHistory(int userId, int limit = 20);

        List<LeaderboardEntryDto> GetLeaderboard(int? setId, int topN = 20);
    }
}