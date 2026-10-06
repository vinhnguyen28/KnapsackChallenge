namespace KnapsackChallenge.Common.DTOs
{
    // 1 dòng bảng xếp hạng: điểm cao nhất mỗi người + % đạt tối ưu.
    public class LeaderboardEntryDto
    {
        public int Rank { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public int BestScore { get; set; }
        public int BestWeight { get; set; }
        public int OptimalValue { get; set; }
        public double OptimalPercent { get; set; }
        public int SetId { get; set; }
        public string SetName { get; set; } = "";
        public System.DateTime? AchievedAt { get; set; }
    }
}