namespace KnapsackChallenge.Common.DTOs
{
    // 1 ván chơi trong lịch sử của người chơi (JOIN RoomPlayers + GameSessions + KnapsackSets).
    public class GameHistoryDto
    {
        public int SessionId { get; set; }
        public string RoomCode { get; set; } = "";
        public string SetName { get; set; } = "";
        public string Difficulty { get; set; } = ""; // Easy / Medium / Hard
        public int TotalScore { get; set; }
        public int TotalWeight { get; set; }
        public int MaxWeight { get; set; }
        public bool IsSubmitted { get; set; }
        public DateTime? CreatedAt { get; set; }

        // ---- Bổ sung v3 ----
        public string Mode { get; set; } = "Solo";
        public int? OptimalValue { get; set; }
        public int? TimeSpentSeconds { get; set; }

        // % đạt tối ưu tính sẵn để UI binding.
        public double OptimalPercent =>
            OptimalValue.HasValue && OptimalValue.Value > 0
                ? (double)TotalScore / OptimalValue.Value * 100.0
                : 0;
    }
}