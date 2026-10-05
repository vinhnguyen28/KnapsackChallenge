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
    }
}