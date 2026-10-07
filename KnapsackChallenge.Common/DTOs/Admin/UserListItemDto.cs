using KnapsackChallenge.Common.Constants;

namespace KnapsackChallenge.Common.DTOs
{
    // DTO cho danh sách người chơi ở màn Admin.
    // TUYỆT ĐỐI KHÔNG chứa PasswordHash -> chỉ tồn tại trong luồng Auth.
    public class UserListItemDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string Role { get; set; } = "";
        public bool IsBanned { get; set; }
        public string? BanReason { get; set; }
        public DateTime? BannedAt { get; set; }
        public string? BannedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime? LastSeenAt { get; set; }

        // Trạng thái suy ra từ dữ liệu (không lưu cờ IsOnline để tránh kẹt Online khi crash).
        public bool IsOnline =>
            !IsBanned &&
            LastSeenAt.HasValue &&
            (DateTime.UtcNow - LastSeenAt.Value).TotalSeconds < AppConfig.OnlineTimeoutSeconds;

        public string StatusText => IsBanned ? "Bị ban" : (IsOnline ? "Online" : "Offline");
    }
}