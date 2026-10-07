namespace KnapsackChallenge.Common.DTOs
{
    // 1 dòng trong bảng "Thống kê theo bộ đề".
    public class SetStatsDto
    {
        public int SetId { get; set; }
        public string SetName { get; set; } = "";
        public string Difficulty { get; set; } = "";
        public int PlayCount { get; set; }
        public double AvgOptimalPercent { get; set; } // 0..100+
        public double OptimalRate { get; set; }        // 0..100
        public int MaxScore { get; set; }
        public string? RecordHolder { get; set; }
    }
}