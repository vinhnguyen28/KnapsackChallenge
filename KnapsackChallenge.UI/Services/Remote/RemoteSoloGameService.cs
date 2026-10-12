using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Services.Player;

namespace KnapsackChallenge.UI.Services.Remote
{
    // Gọi REST Server cho toàn bộ luồng Solo.
    // - Server lấy userId từ claim "sub" → tham số userId ở đây BỊ BỎ QUA.
    // - Lỗi validate (400) → trả tuple (Success=false, Message) để ViewModel hiển thị.
    // - Lỗi 401/403/5xx hoặc mạng → để ApiException / ServerUnavailableException nổi lên
    //   (ViewModel tự quyết định catch hay không).
    public sealed class RemoteSoloGameService : ISoloGameService
    {
        private const int MaxSubmitItemIds = 500;
        private const int MaxTimeSpentSeconds = 86400;
        private const int MaxHistoryLimit = 200;
        private const int MaxLeaderboardTopN = 100;

        public (bool IsEnabled, int TimeLimitSeconds) GetSoloModeStatus()
        {
            var dto = ApiClient.Get<SoloModeStatusDto>("api/solo/status");
            return (dto?.IsEnabled ?? false, dto?.TimeLimitSeconds ?? 0);
        }

        public List<SoloGameSetDto> GetAvailableSets()
        {
            return ApiClient.Get<List<SoloGameSetDto>>("api/solo/sets") ?? new();
        }

        public (bool Success, string Message, SoloGameDataDto? Data, HeartStatusDto? HeartStatus)
            StartGame(int userId, int setId)
        {
            // userId bị bỏ qua — server lấy từ token.
            try
            {
                var resp = ApiClient.Post<StartGameRequestDto, StartGameResponseDto>(
                    "api/solo/start",
                    new StartGameRequestDto { SetId = setId });

                return (resp.Success, resp.Message, resp.Data, resp.HeartStatus);
            }
            catch (ApiException ex) when (ex.StatusCode == 400)
            {
                // Validate sai (SetId <= 0) — trả như lỗi nghiệp vụ để UI hiển thị.
                return (false, ex.Message, null, null);
            }
        }

        public (bool Success, string Message, SoloResultDto? Result) Submit(
            int userId,
            int setId,
            IEnumerable<int> selectedItemIds,
            int timeSpentSeconds,
            bool isTimeout = false)
        {
            // userId bị bỏ qua — server lấy từ token.

            // Chuẩn hoá trước khi gửi — server vẫn validate lại, nhưng tránh
            // gửi payload khổng lồ vô ích.
            var ids = selectedItemIds?.Distinct().ToList() ?? new List<int>();
            if (ids.Count > MaxSubmitItemIds)
                ids = ids.Take(MaxSubmitItemIds).ToList();

            if (timeSpentSeconds < 0) timeSpentSeconds = 0;
            if (timeSpentSeconds > MaxTimeSpentSeconds) timeSpentSeconds = MaxTimeSpentSeconds;

            try
            {
                var req = new SubmitSoloRequestDto
                {
                    SetId = setId,
                    SelectedItemIds = ids,
                    TimeSpentSeconds = timeSpentSeconds,
                    IsTimeout = isTimeout,
                };

                var resp = ApiClient.Post<SubmitSoloRequestDto, ApiResult<SoloResultDto>>(
                    "api/solo/submit", req);

                return (resp.Success, resp.Message, resp.Data);
            }
            catch (ApiException ex) when (ex.StatusCode == 400)
            {
                return (false, ex.Message, null);
            }
        }

        public List<GameHistoryDto> GetHistory(int userId, int limit = 20)
        {
            // userId bị bỏ qua — server lấy từ token.
            if (limit < 1) limit = 1;
            if (limit > MaxHistoryLimit) limit = MaxHistoryLimit;

            return ApiClient.Get<List<GameHistoryDto>>(
                $"api/solo/history?limit={limit}") ?? new();
        }

        public List<LeaderboardEntryDto> GetLeaderboard(int? setId, int topN = 20)
        {
            if (topN < 1) topN = 1;
            if (topN > MaxLeaderboardTopN) topN = MaxLeaderboardTopN;

            var path = setId.HasValue
                ? $"api/solo/leaderboard?setId={setId.Value}&topN={topN}"
                : $"api/solo/leaderboard?topN={topN}";

            return ApiClient.Get<List<LeaderboardEntryDto>>(path) ?? new();
        }
    }
}