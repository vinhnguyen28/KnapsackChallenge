namespace KnapsackChallenge.Common.DTOs
{
    public class RoomPlayerDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public bool IsHost { get; set; }
        public bool IsOnline { get; set; }
        public bool IsSubmitted { get; set; }
        public bool IsKicked { get; set; }
        public int TotalScore { get; set; }
        public int TotalWeight { get; set; }
        public int? TimeSpentSeconds { get; set; }
        public DateTime JoinedAt { get; set; }
    }
}