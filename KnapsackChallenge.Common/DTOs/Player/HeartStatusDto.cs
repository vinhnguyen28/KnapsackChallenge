namespace KnapsackChallenge.Common.DTOs
{
    // Trạng thái tim hiện tại của người chơi.
    // NextRefillAtUtc = null khi đang ở mức tối đa (không có gì để hồi).
    public record HeartStatusDto(
        int Current,
        int Max,
        DateTime? NextRefillAtUtc,
        int RefillIntervalMinutes)
    {
        public bool IsFull => Current >= Max;
        public bool IsEmpty => Current <= 0;

        // Số giây còn lại đến tim kế tiếp (0 nếu đã đầy).
        public int SecondsUntilNextRefill =>
            NextRefillAtUtc.HasValue
                ? (int)Math.Max(0, (NextRefillAtUtc.Value - DateTime.UtcNow).TotalSeconds)
                : 0;

        // Chuỗi hiển thị kiểu "25:00" cho countdown.
        public string CountdownText
        {
            get
            {
                var s = SecondsUntilNextRefill;
                var ts = TimeSpan.FromSeconds(s);
                return ts.TotalHours >= 1
                    ? $"{(int)ts.TotalHours}h{ts.Minutes:D2}m"
                    : ts.ToString(@"mm\:ss");
            }
        }

        // Chuỗi hiển thị badge header: "❤ 10/25".
        public string BadgeText => $"❤ {Current}/{Max}";
    }
}