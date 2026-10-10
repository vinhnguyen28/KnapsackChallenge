using KnapsackChallenge.Common.DTOs;

namespace KnapsackChallenge.Core.Services.Player
{
    public interface IPlayerSessionService
    {
        // Gọi mỗi 30s: cập nhật LastSeenAt + hồi tim. Trả trạng thái tim hiện tại.
        HeartStatusDto Heartbeat(int userId);

        // Đọc trạng thái tim (không đụng LastSeenAt). Tự động hồi nếu cần.
        HeartStatusDto GetHeartStatus(int userId);

        // Gọi khi đăng xuất / đóng app: LastSeenAt = NULL.
        void GoOffline(int userId);

        // Người chơi đang chơi mà bị ban -> phát hiện ở lần heartbeat kế tiếp.
        (bool Banned, string? Reason) CheckStatus(int userId);
    }
}