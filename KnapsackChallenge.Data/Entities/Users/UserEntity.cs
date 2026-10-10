namespace KnapsackChallenge.Data.Entities
{
    public class UserEntity
    {
        public int Id { get; set; }
        public string? Username { get; set; }
        public string? PasswordHash { get; set; }
        public string? Role { get; set; }

        // ---- Ban ----
        public bool IsBanned { get; set; }
        public string? BanReason { get; set; }
        public DateTime? BannedAt { get; set; }
        public string? BannedBy { get; set; }

        // ---- Vòng đời tài khoản / heartbeat ----
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime? LastSeenAt { get; set; }

        // ---- v7: Hệ thống tim ----
        public int Hearts { get; set; }
        public DateTime? LastHeartRefillAt { get; set; }
    }
}