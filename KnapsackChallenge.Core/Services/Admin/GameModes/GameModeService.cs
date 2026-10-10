using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.Data.Repositories;

namespace KnapsackChallenge.Core.Services.Admin
{
    public class GameModeService : IGameModeService
    {
        private const int MaxTimeLimitSeconds = 3600;
        private const int MinMultiplayers = 2;
        private const int MaxMultiplayers = 10;
        private const int MinHearts = 1;
        private const int MaxHearts = 999;
        private const int MinRefillMinutes = 1;
        private const int MaxRefillMinutes = 1440; // 24h

        private readonly GameModeRepository _repository;

        public GameModeService(GameModeRepository repository)
        {
            _repository = repository;
        }

        public List<GameModeEntity> GetAll() => _repository.GetAll();
        public GameModeEntity? GetByKey(string modeKey) => _repository.GetByKey(modeKey);

        public bool IsEnabled(string modeKey)
        {
            var m = _repository.GetByKey(modeKey);
            return m?.IsEnabled ?? false;
        }

        public (bool Success, string Message) Update(string modeKey,
                                                     bool isEnabled,
                                                     int timeLimitSeconds,
                                                     int? maxPlayers,
                                                     int? maxHearts,
                                                     int? heartRefillMinutes,
                                                     UserEntity currentAdmin)
        {
            var mode = _repository.GetByKey(modeKey);
            if (mode == null)
                return (false, $"Không tìm thấy chế độ '{modeKey}'.");

            if (timeLimitSeconds < 0 || timeLimitSeconds > MaxTimeLimitSeconds)
                return (false, $"Giới hạn thời gian phải trong khoảng 0..{MaxTimeLimitSeconds} giây!");

            bool isMultiplayer = string.Equals(modeKey, "Multiplayer", StringComparison.OrdinalIgnoreCase);
            bool isSolo = string.Equals(modeKey, "Solo", StringComparison.OrdinalIgnoreCase);

            // MaxPlayers chỉ hợp lệ với Multiplayer.
            if (isMultiplayer)
            {
                if (maxPlayers.HasValue && (maxPlayers.Value < MinMultiplayers || maxPlayers.Value > MaxMultiplayers))
                    return (false, $"Số người tối đa phải trong khoảng {MinMultiplayers}..{MaxMultiplayers}!");
            }
            else
            {
                maxPlayers = null;
            }

            // Tim chỉ hợp lệ với Solo.
            if (isSolo)
            {
                if (maxHearts.HasValue && (maxHearts.Value < MinHearts || maxHearts.Value > MaxHearts))
                    return (false, $"Số tim tối đa phải trong khoảng {MinHearts}..{MaxHearts}!");

                if (heartRefillMinutes.HasValue
                    && (heartRefillMinutes.Value < MinRefillMinutes || heartRefillMinutes.Value > MaxRefillMinutes))
                    return (false, $"Thời gian hồi tim phải trong khoảng {MinRefillMinutes}..{MaxRefillMinutes} phút!");
            }
            else
            {
                maxHearts = null;
                heartRefillMinutes = null;
            }

            var adminName = currentAdmin.Username ?? "";
            bool ok = _repository.Update(modeKey, isEnabled, timeLimitSeconds,
                                          maxPlayers, maxHearts, heartRefillMinutes, adminName);

            return ok
                ? (true, $"Đã lưu cấu hình chế độ {mode.DisplayName}!")
                : (false, "Không lưu được cấu hình (chế độ có thể đã bị xóa).");
        }
    }
}