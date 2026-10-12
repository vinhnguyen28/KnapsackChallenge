namespace KnapsackChallenge.Common.DTOs
{
    // Trạng thái chế độ Solo trả cho client qua GET /api/solo/status.
    public class SoloModeStatusDto
    {
        public bool IsEnabled { get; set; }
        public int TimeLimitSeconds { get; set; }
    }
}