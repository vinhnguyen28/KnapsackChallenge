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

        // v8: thông tin EXP nhận được (null nếu cộng EXP thất bại — không ảnh hưởng kết quả ván).
        public int ExpGained { get; set; }
        public int NewLevel { get; set; }
        public int OldLevel { get; set; }
        public bool LeveledUp => NewLevel > OldLevel;
        public string RankTitle { get; set; } = "";

        // Chuỗi hiển thị "+120 EXP" hoặc "".
        public string ExpGainText => ExpGained > 0 ? $"+{ExpGained} EXP" : "";

        // "Lên cấp! Lv 5 · Bạc" nếu leveled up.
        public string LevelUpText => LeveledUp
            ? $"Lên cấp! Lv {NewLevel} · {RankTitle}"
            : "";
    }
}