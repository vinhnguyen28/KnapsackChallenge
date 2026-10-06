using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.Data.Repositories;

namespace KnapsackChallenge.Core.Services.Admin
{
    public class GameModeService : IGameModeService
    {
        private const int MaxTimeLimitSeconds = 3600;
        private const int MinMultiplayers = 2;
        private const int MaxMultiplayers = 10;

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
                                                     UserEntity currentAdmin)
        {
            var mode = _repository.GetByKey(modeKey);
            if (mode == null)
                return (false, $"Không tìm thấy chế độ '{modeKey}'.");

            if (timeLimitSeconds < 0 || timeLimitSeconds > MaxTimeLimitSeconds)
                return (false, $"Giới hạn thời gian phải trong khoảng 0..{MaxTimeLimitSeconds} giây!");

            // MaxPlayers chỉ hợp lệ với Multiplayer.
            if (string.Equals(modeKey, "Multiplayer", StringComparison.OrdinalIgnoreCase))
            {
                if (maxPlayers.HasValue && (maxPlayers.Value < MinMultiplayers || maxPlayers.Value > MaxMultiplayers))
                    return (false, $"Số người tối đa phải trong khoảng {MinMultiplayers}..{MaxMultiplayers}!");
            }
            else
            {
                maxPlayers = null;
            }

            var adminName = currentAdmin.Username ?? "";
            bool ok = _repository.Update(modeKey, isEnabled, timeLimitSeconds, maxPlayers, adminName);

            return ok
                ? (true, $"Đã lưu cấu hình chế độ {mode.DisplayName}!")
                : (false, "Không lưu được cấu hình (chế độ có thể đã bị xóa).");
        }
    }
}