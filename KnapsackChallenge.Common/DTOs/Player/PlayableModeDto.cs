namespace KnapsackChallenge.Common.DTOs
{
    // Thông tin chế độ chơi mà Player được phép biết.
    // KHÔNG chứa UpdatedBy/UpdatedAt (những field chỉ Admin cần).
    public class PlayableModeDto
    {
        public string ModeKey { get; set; } = "";
        public bool IsEnabled { get; set; }
        public int TimeLimitSeconds { get; set; }
        public int? MaxPlayers { get; set; }
    }
}