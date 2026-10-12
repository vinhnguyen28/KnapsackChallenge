namespace KnapsackChallenge.Common.DTOs
{
    // 1 dòng bảng xếp hạng theo EXP toàn hệ thống.
    // RankTitle được tầng Core điền (dùng RankRules) — Data không biết Core.
    public class ExpLeaderboardEntryDto
    {
        public int Rank { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public long TotalExp { get; set; }
        public int Level { get; set; } = 1;
        public string RankTitle { get; set; } = "";      // Core điền
        public string LevelBadge => $"Lv {Level} · {RankTitle}";
    }
}