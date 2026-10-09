namespace KnapsackChallenge.Common.DTOs
{
    public class FinalRankingEntryDto
    {
        public int Rank { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public int TotalScore { get; set; }
        public int TotalWeight { get; set; }
        public int OptimalValue { get; set; }
        public double OptimalPercent { get; set; }
        public int TimeSpentSeconds { get; set; }
        public bool IsSubmitted { get; set; }
        public bool IsKicked { get; set; }
        public bool IsBanned { get; set; }
        public bool IsLeft { get; set; }   // ← thêm dòng này
    }
}