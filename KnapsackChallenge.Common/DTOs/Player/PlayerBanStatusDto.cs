namespace KnapsackChallenge.Common.DTOs
{
    // Trạng thái ban của chính người chơi đang đăng nhập (endpoint /api/player/status).
    // Dùng cho heartbeat poll: client phát hiện bị ban để logout.
    public class PlayerBanStatusDto
    {
        public bool Banned { get; set; }
        public string? Reason { get; set; }
    }
}