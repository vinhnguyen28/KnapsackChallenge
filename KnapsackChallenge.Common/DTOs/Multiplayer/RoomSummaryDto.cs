using KnapsackChallenge.Common.Enums;

namespace KnapsackChallenge.Common.DTOs
{
    public class RoomSummaryDto
    {
        public string RoomCode { get; set; } = "";
        public int SessionId { get; set; }
        public RoomStatus Status { get; set; }
        public string HostUsername { get; set; } = "";
        public string SetName { get; set; } = "";
        public int PlayerCount { get; set; }
        public int MaxPlayers { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? StartedAtUtc { get; set; }

        // Bổ sung cho Lobby: hiển thị độ khó + thời gian của phòng ngay trên danh sách.
        public string Difficulty { get; set; } = "";
        public int TimeLimitSeconds { get; set; }
    }
}