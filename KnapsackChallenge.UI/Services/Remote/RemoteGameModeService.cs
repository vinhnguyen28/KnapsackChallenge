using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Services.Admin;
using KnapsackChallenge.Data.Entities;

namespace KnapsackChallenge.UI.Services.Remote
{
    // Đọc trạng thái chế độ chơi qua REST Server.
    // - GetAll/GetByKey/IsEnabled: dùng endpoint GET api/modes.
    // - Update: KHÔNG hỗ trợ — chỉ Admin sửa được, sẽ làm ở bước sau.
    // - KHÔNG nuốt ApiException/ServerUnavailableException — để caller quyết định.
    public sealed class RemoteGameModeService : IGameModeService
    {
        // =========================================================
        // v12: CACHE — modes gần như không đổi (Admin sửa vài lần/năm).
        // ---------------------------------------------------------
        // HomePageViewModel.LoadModeStatusAsync gọi GetAll() mỗi lần
        // vào Home -> nếu không cache sẽ khựng ~200-500ms mỗi lần.
        // Cache 10 phút đủ an toàn vì Admin hiếm khi đổi.
        //
        // TTL dài hơn sets (10 phút vs 5 phút) vì modes thay đổi ít
        // hơn sets rất nhiều.
        // =========================================================
        private static List<GameModeEntity>? _cachedModes;
        private static DateTime _cacheExpiryUtc = DateTime.MinValue;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
        private static readonly object _cacheLock = new();

        public List<GameModeEntity> GetAll()
        {
            // Cache hit -> trả bản copy (caller có thể mutate).
            lock (_cacheLock)
            {
                if (_cachedModes != null && DateTime.UtcNow < _cacheExpiryUtc)
                    return CloneModes(_cachedModes);
            }

            var list = ApiClient.Get<List<PlayableModeDto>>("api/modes");
            var modes = list == null
                ? new List<GameModeEntity>()
                : list.Select(dto => new GameModeEntity
                {
                    ModeKey = dto.ModeKey,
                    DisplayName = dto.ModeKey,
                    IsEnabled = dto.IsEnabled,
                    TimeLimitSeconds = dto.TimeLimitSeconds,
                    MaxPlayers = dto.MaxPlayers,
                }).ToList();

            lock (_cacheLock)
            {
                _cachedModes = modes;
                _cacheExpiryUtc = DateTime.UtcNow.Add(CacheTtl);
            }

            return CloneModes(modes);
        }

        public GameModeEntity? GetByKey(string modeKey)
        {
            if (string.IsNullOrWhiteSpace(modeKey)) return null;

            return GetAll().FirstOrDefault(m =>
                string.Equals(m.ModeKey, modeKey, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsEnabled(string modeKey) => GetByKey(modeKey)?.IsEnabled ?? false;

        public (bool Success, string Message) Update(string modeKey,
                                                     bool isEnabled,
                                                     int timeLimitSeconds,
                                                     int? maxPlayers,
                                                     int? maxHearts,
                                                     int? heartRefillMinutes,
                                                     UserEntity currentAdmin)
            => throw new NotSupportedException(
                "Cập nhật chế độ chơi chưa hỗ trợ qua API (chỉ Admin, sẽ làm ở bước sau).");

        // =========================================================
        // Cache invalidation
        // =========================================================

        public static void InvalidateCache()
        {
            lock (_cacheLock)
            {
                _cachedModes = null;
                _cacheExpiryUtc = DateTime.MinValue;
            }
        }

        // =========================================================
        // Helpers
        // =========================================================

        // Trả bản copy sâu — tránh caller (HomePageViewModel) mutate
        // trực tiếp entity trong cache (dù hiện tại chưa mutate, phòng
        // ngừa tương lai).
        private static List<GameModeEntity> CloneModes(List<GameModeEntity> source)
        {
            var copy = new List<GameModeEntity>(source.Count);
            foreach (var m in source)
            {
                copy.Add(new GameModeEntity
                {
                    ModeKey = m.ModeKey,
                    DisplayName = m.DisplayName,
                    IsEnabled = m.IsEnabled,
                    TimeLimitSeconds = m.TimeLimitSeconds,
                    MaxPlayers = m.MaxPlayers,
                    MaxHearts = m.MaxHearts,
                    HeartRefillMinutes = m.HeartRefillMinutes,
                    UpdatedAt = m.UpdatedAt,
                    UpdatedBy = m.UpdatedBy,
                });
            }
            return copy;
        }
    }
}