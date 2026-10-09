namespace KnapsackChallenge.Common.DTOs
{
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