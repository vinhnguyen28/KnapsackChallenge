
namespace KnapsackChallenge.Data.Entities
{
    public class UserEntity
    {
        public int Id { get; set; }
        public string? Username { get; set; }
        public string? PasswordHash { get; set; }
        public string? Role { get; set; } // 'Admin' hoặc 'Player'

        // ---- Ban ----
        public bool IsBanned { get; set; }
        public string? BanReason { get; set; }
        public DateTime? BannedAt { get; set; }
        public string? BannedBy { get; set; } // username admin đã ban

        // ---- Vòng đời tài khoản / heartbeat ----
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime? LastSeenAt { get; set; } // NULL = đã đăng xuất hẳn
    }
}