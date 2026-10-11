namespace KnapsackChallenge.Common.DTOs
{
    // Trạng thái Rank/EXP hiện tại của người chơi — dùng cho HomePage + header badge.
    public class RankStatusDto
    {
        public long TotalExp { get; set; }
        public int Level { get; set; } = 1;
        public string RankTitle { get; set; } = "Đồng";

        // Tiến độ trong level hiện tại (0..RangeInLevel).
        public long CurrentInLevel { get; set; }
        public long RangeInLevel { get; set; } = 1;

        // 0..100, sẵn sàng bind ProgressBar.
        public double ProgressPercent =>
            RangeInLevel <= 0 ? 0 : (double)CurrentInLevel / RangeInLevel * 100.0;

        // Chuỗi hiển thị: "Lv 5 · Bạc".
        public string LevelBadge => $"Lv {Level} · {RankTitle}";

        // "120 / 300 EXP" cho tooltip/label.
        public string ProgressText => $"{CurrentInLevel} / {RangeInLevel} EXP";
    }

    // Kết quả sau khi cộng EXP — để hiển thị "+XX EXP" ở Result view.
    public class ExpAwardResultDto
    {
        public int ExpGained { get; set; }
        public long NewTotalExp { get; set; }
        public int NewLevel { get; set; }
        public int OldLevel { get; set; }
        public bool LeveledUp => NewLevel > OldLevel;
        public string RankTitle { get; set; } = "";

        // "Lên cấp! Lv 5 · Bạc" khi LeveledUp, ngược lại rỗng.
        public string LevelUpText => LeveledUp
            ? $"Lên cấp! Lv {NewLevel} · {RankTitle}"
            : "";
    }
}