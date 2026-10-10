namespace KnapsackChallenge.Data.Entities
{
    public class GameModeEntity
    {
        public string ModeKey { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public bool IsEnabled { get; set; }
        public int TimeLimitSeconds { get; set; }
        public int? MaxPlayers { get; set; }         // chỉ dùng cho Multiplayer

        // ---- v7: cấu hình tim (chỉ áp dụng cho Solo) ----
        public int? MaxHearts { get; set; }
        public int? HeartRefillMinutes { get; set; }

        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}