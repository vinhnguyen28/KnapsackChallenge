using KnapsackChallenge.Common.Enums;

namespace KnapsackChallenge.Common.DTOs
{
    public class RoomStateDto
    {
        public string RoomCode { get; set; } = "";
        public int SessionId { get; set; }
        public RoomStatus Status { get; set; }
        public int HostUserId { get; set; }
        public int SetId { get; set; }
        public string SetName { get; set; } = "";
        public string Difficulty { get; set; } = "";
        public int MaxWeight { get; set; }
        public int MaxPlayers { get; set; }
        public int TimeLimitSeconds { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? EndTimeUtc { get; set; }
        public List<RoomPlayerDto> Players { get; set; } = new();
    }
}