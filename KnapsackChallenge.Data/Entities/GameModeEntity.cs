namespace KnapsackChallenge.Data.Entities
{
    // Cấu hình 1 chế độ chơi.
    public class GameModeEntity
    {
        public string ModeKey { get; set; } = "";       // 'Solo' | 'Multiplayer'
        public string DisplayName { get; set; } = "";
        public bool IsEnabled { get; set; }
        public int TimeLimitSeconds { get; set; }       // 0 = không giới hạn
        public int? MaxPlayers { get; set; }            // chỉ dùng cho Multiplayer
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}