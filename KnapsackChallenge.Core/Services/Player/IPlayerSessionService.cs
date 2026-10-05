namespace KnapsackChallenge.Core.Services.Player
{
    // Heartbeat phiên chơi + kiểm tra trạng thái ban của chính mình.
    public interface IPlayerSessionService
    {
        // Gọi mỗi 30s: cập nhật LastSeenAt = now.
        void Heartbeat(int userId);

        // Gọi khi đăng xuất / đóng app: LastSeenAt = NULL.
        void GoOffline(int userId);

        // Người chơi đang chơi mà bị ban -> phát hiện ở lần heartbeat kế tiếp.
        (bool Banned, string? Reason) CheckStatus(int userId);
    }
}