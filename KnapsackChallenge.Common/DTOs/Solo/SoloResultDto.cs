using System.Collections.Generic;

namespace KnapsackChallenge.Common.DTOs
{
    // Kết quả sau khi nộp bài (server-side tính lại toàn bộ).
    // Stars: 100% = 3, >=90% = 2, >=70% = 1, còn lại 0.
    public class SoloResultDto
    {
        public int Score { get; set; }
        public int TotalWeight { get; set; }
        public int MaxWeight { get; set; }
        public int OptimalValue { get; set; }
        public double OptimalPercent { get; set; }
        public int Stars { get; set; }
        public List<int> OptimalItemIds { get; set; } = new();

        // Chuỗi sao tiện binding (VD: ★★☆).
        public string StarText =>
            new string('★', Stars) + new string('☆', System.Math.Max(0, 3 - Stars));
    }
}