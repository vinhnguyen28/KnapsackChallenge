namespace KnapsackChallenge.Common.DTOs
{
    // Thẻ tổng quan trang Thống kê.
    // Chỉ tính ván IsSubmitted = 1 VÀ OptimalValue IS NOT NULL AND > 0.
    public class AdminStatsOverviewDto
    {
        public int TotalGames { get; set; }          // tổng ván hợp lệ
        public int OptimalGames { get; set; }        // số ván TotalScore = OptimalValue
        public double OptimalRate { get; set; }      // 0..100
        public double AvgOptimalPercent { get; set; }// 0..100+ (có thể >100 nếu dữ liệu lạ)
        public int MaxScore { get; set; }
        public string? MaxScoreHolder { get; set; }  // username người giữ kỷ lục (null nếu chưa có)
    }
}