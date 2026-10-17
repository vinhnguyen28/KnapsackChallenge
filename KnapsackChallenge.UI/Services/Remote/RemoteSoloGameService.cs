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

        // =========================================================
        // v12: CACHE — giảm REST call cho dữ liệu ít thay đổi.
        // ---------------------------------------------------------
        // BỐI CẢNH:
        //   Mỗi lần vào màn "Chọn mức độ" (SetSelectionViewModel) đều
        //   gọi GetAvailableSets() qua HTTP -> ~200-500ms. Người chơi
        //   cảm giác "khựng" vài giây mỗi lần đổi qua lại giữa Home
        //   và SetSelection.
        //
        // GIẢI PHÁP:
        //   Cache danh sách bộ đề + trạng thái mode trong 5 phút.
        //   Lần đầu chờ 1 lần, các lần sau trong 5 phút trả instant.
        //
        // TẠI SAO 5 PHÚT:
        //   - Bộ đề do Admin tạo/sửa, tần suất rất thấp (vài lần/tuần
        //     hoặc ít hơn).
        //   - Nếu Admin sửa xong mà client còn cache -> người chơi thấy
        //     data cũ tối đa 5 phút. Chấp nhận được.
        //   - Nếu cần ép refresh: gọi InvalidateSetsCache() (VD: sau
        //     khi Admin thao tác - không cần vì Admin dùng app riêng).
        //
        // TẠI SAO STATIC:
        //   - RemoteSoloGameService là singleton (qua ClientServiceFactory
        //     ._soloGameService.Value). Cache instance cũng được.
        //   - Nhưng dùng static giúp an toàn nếu sau này ai đó tạo
        //     instance mới — cache vẫn chia sẻ.
        //
        // THREAD-SAFE:
        //   - lock đơn giản bảo vệ _cacheLock. Số caller đồng thời rất
        //     nhỏ (chỉ HomePageViewModel + SetSelectionViewModel), lock
        //     contention không đáng kể.
        // =========================================================
        private static List<SoloGameSetDto>? _cachedSets;
        private static DateTime _cacheExpiryUtc = DateTime.MinValue;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
        private static readonly object _cacheLock = new();

        // Cache trạng thái Solo (enabled + timeLimit) — cũng rất ít đổi.
        private static (bool IsEnabled, int TimeLimitSeconds)? _cachedStatus;
        private static DateTime _statusCacheExpiryUtc = DateTime.MinValue;

        // =========================================================
        // PUBLIC API (giữ nguyên contract ISoloGameService)
        // =========================================================

        public (bool IsEnabled, int TimeLimitSeconds) GetSoloModeStatus()
        {
            // Cache hit -> trả luôn.
            lock (_cacheLock)
            {
                if (_cachedStatus.HasValue && DateTime.UtcNow < _statusCacheExpiryUtc)
                    return _cachedStatus.Value;
            }

            var dto = ApiClient.Get<SoloModeStatusDto>("api/solo/status");
            var result = (dto?.IsEnabled ?? false, dto?.TimeLimitSeconds ?? 0);

            lock (_cacheLock)
            {
                _cachedStatus = result;
                _statusCacheExpiryUtc = DateTime.UtcNow.Add(CacheTtl);
            }

            return result;
        }

        public List<SoloGameSetDto> GetAvailableSets()
        {
            // Cache hit -> trả luôn. Trả bản copy để caller không vô
            // tình mutate cache qua tham chiếu (ObservableCollection
            // trong VM có thể Add/Remove trực tiếp list trả về).
            lock (_cacheLock)
            {
                if (_cachedSets != null && DateTime.UtcNow < _cacheExpiryUtc)
                    return new List<SoloGameSetDto>(_cachedSets);
            }

            var list = ApiClient.Get<List<SoloGameSetDto>>("api/solo/sets") ?? new();

            lock (_cacheLock)
            {
                _cachedSets = list;
                _cacheExpiryUtc = DateTime.UtcNow.Add(CacheTtl);
            }

            return new List<SoloGameSetDto>(list);
        }

        // =========================================================
        // Cache invalidation (public để caller chủ động gọi nếu cần)
        // =========================================================

        // Xoá cache bộ đề + trạng thái mode. Gọi sau khi Admin thao
        // tác (không cần trong luồng Player bình thường) hoặc khi
        // người chơi bấm nút "Làm mới" tường minh.
        public static void InvalidateCache()
        {
            lock (_cacheLock)
            {
                _cachedSets = null;
                _cacheExpiryUtc = DateTime.MinValue;
                _cachedStatus = null;
                _statusCacheExpiryUtc = DateTime.MinValue;
            }
        }

        public (bool Success, string Message, SoloGameDataDto? Data, HeartStatusDto? HeartStatus)
            StartGame(int userId, int setId)
        {
            // userId bị bỏ qua — server lấy từ token.
            // KHÔNG cache: mỗi lần StartGame là ván mới, cần data tươi.
            try
            {
                var resp = ApiClient.Post<StartGameRequestDto, StartGameResponseDto>(
                    "api/solo/start",
                    new StartGameRequestDto { SetId = setId });

                return (resp.Success, resp.Message, resp.Data, resp.HeartStatus);
            }
            catch (ApiException ex) when (ex.StatusCode == 400)
            {
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
            // KHÔNG cache: lịch sử thay đổi sau mỗi ván.
            if (limit < 1) limit = 1;
            if (limit > MaxHistoryLimit) limit = MaxHistoryLimit;

            return ApiClient.Get<List<GameHistoryDto>>(
                $"api/solo/history?limit={limit}") ?? new();
        }

        public List<LeaderboardEntryDto> GetLeaderboard(int? setId, int topN = 20)
        {
            // KHÔNG cache: BXH thay đổi liên tục.
            if (topN < 1) topN = 1;
            if (topN > MaxLeaderboardTopN) topN = MaxLeaderboardTopN;

            var path = setId.HasValue
                ? $"api/solo/leaderboard?setId={setId.Value}&topN={topN}"
                : $"api/solo/leaderboard?topN={topN}";

            return ApiClient.Get<List<LeaderboardEntryDto>>(path) ?? new();
        }
    }
}