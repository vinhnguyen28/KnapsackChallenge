namespace KnapsackChallenge.Common.DTOs
{
    public class SubmissionResultDto
    {
        public int Score { get; set; }
        public int TotalWeight { get; set; }
        public int MaxWeight { get; set; }
        public int OptimalValue { get; set; }
        public double OptimalPercent { get; set; }
        public int Stars { get; set; }
        public int TimeSpentSeconds { get; set; }
        public bool IsAutoSubmitted { get; set; }
        public List<int> OptimalItemIds { get; set; } = new();
    }
}