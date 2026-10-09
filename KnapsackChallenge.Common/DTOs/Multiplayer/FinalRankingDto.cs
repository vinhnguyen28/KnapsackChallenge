namespace KnapsackChallenge.Common.DTOs
{
    // Payload cuối ván — server đẩy qua event "GameEnded".
    // Là container chứa danh sách FinalRankingEntryDto (1 dòng / người chơi).
    public class FinalRankingDto
    {
        public string RoomCode { get; set; } = "";
        public int SessionId { get; set; }
        public int SetId { get; set; }
        public string SetName { get; set; } = "";
        public int OptimalValue { get; set; }
        public DateTime FinishedAtUtc { get; set; }
        public List<FinalRankingEntryDto> Entries { get; set; } = new();
    }
}