namespace KnapsackChallenge.Common.DTOs
{
    // Tổng hợp thành tích 1 người chơi (panel "Xem thành tích").
    public class PlayerAchievementDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string Role { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }

        public bool IsBanned { get; set; }
        public string? BanReason { get; set; }
        public DateTime? BannedAt { get; set; }
        public string? BannedBy { get; set; }

        public int TotalGames { get; set; }
        public int SubmittedGames { get; set; }
        public int HighestScore { get; set; }
        public double AverageScore { get; set; }
        public int TotalScore { get; set; }

        public List<GameHistoryDto> RecentGames { get; set; } = new();
    }
}