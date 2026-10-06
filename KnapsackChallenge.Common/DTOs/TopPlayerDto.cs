namespace KnapsackChallenge.Common.DTOs
{
    // Top người chơi (tiêu chí: BestScore DESC).
    public class TopPlayerDto
    {
        public int Rank { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public int BestScore { get; set; }
        public double AvgOptimalPercent { get; set; }
        public int GamesPlayed { get; set; }
        public double OptimalRate { get; set; }
    }
}