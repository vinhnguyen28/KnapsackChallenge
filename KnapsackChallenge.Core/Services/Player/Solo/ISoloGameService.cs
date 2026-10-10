using KnapsackChallenge.Common.DTOs;

namespace KnapsackChallenge.Core.Services.Player
{
    public interface ISoloGameService
    {
        (bool IsEnabled, int TimeLimitSeconds) GetSoloModeStatus();

        List<SoloGameSetDto> GetAvailableSets();

        // StartGame: tiêu tốn 1 tim nếu thành công.
        // Trả kèm HeartStatusDto để UI hiển thị tim hiện tại + countdown.
        // Khi hết tim: Success=false, Message=thông báo, Data=null,
        //             HeartStatus=trạng thái tim (Current=0).
        (bool Success, string Message, SoloGameDataDto? Data, HeartStatusDto? HeartStatus)
            StartGame(int userId, int setId);

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