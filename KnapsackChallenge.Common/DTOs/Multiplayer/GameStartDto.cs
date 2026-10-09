namespace KnapsackChallenge.Common.DTOs
{
    // Payload server đẩy cho mọi người khi bắt đầu ván.
    public class GameStartDto
    {
        public string RoomCode { get; set; } = "";
        public int SessionId { get; set; }
        public int SetId { get; set; }
        public string SetName { get; set; } = "";
        public string Difficulty { get; set; } = "";
        public int MaxWeight { get; set; }
        public int TimeLimitSeconds { get; set; }  // 0 = không giới hạn
        public DateTime StartTimeUtc { get; set; }
        public DateTime? EndTimeUtc { get; set; }  // null nếu không giới hạn
        public List<ItemDto> Items { get; set; } = new();
    }
}