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
        public List<GameModeEntity> GetAll()
        {
            var list = ApiClient.Get<List<PlayableModeDto>>("api/modes");
            if (list == null) return new List<GameModeEntity>();

            return list.Select(dto => new GameModeEntity
            {
                ModeKey = dto.ModeKey,
                // Server không trả DisplayName — dùng ModeKey làm nhãn hiển thị.
                DisplayName = dto.ModeKey,
                IsEnabled = dto.IsEnabled,
                TimeLimitSeconds = dto.TimeLimitSeconds,
                MaxPlayers = dto.MaxPlayers,
                // MaxHearts/HeartRefillMinutes/UpdatedAt/UpdatedBy để mặc định.
            }).ToList();
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
    }
}