namespace KnapsackChallenge.Core.Algorithms
{
    // Quy tắc tính EXP / Level / Rank. Pure static → test được không cần DB.
    // - MinExpForLevel(n) = 50 * (n - 1) * n  (quadratic, tăng dần đều)
    // - Rank title cố định theo Level.
    // - ExpGain công thức:
    //     Base 20 + (int)OptimalPercent + Stars*30 + (OptimalPercent >= 100 ? 50 : 0)
    public static class RankRules
    {
        public const long ExpPerLevelMultiplier = 50;
        public const int MaxLevel = 999; // chặn trên, đủ rộng

        // ---------- Level thresholds ----------

        // Tổng EXP tối thiểu để đạt level n (n >= 1). Lv 1 = 0.
        public static long MinExpForLevel(int level)
        {
            if (level <= 1) return 0L;
            return ExpPerLevelMultiplier * (long)(level - 1) * level;
        }

        // Tổng EXP tối thiểu để đạt level kế tiếp.
        public static long MinExpForNextLevel(int currentLevel) =>
            MinExpForLevel(currentLevel + 1);

        // Tính level từ tổng EXP hiện có.
        // Giải phương trình n^2 - n - Exp/50 = 0 với n = (1 + sqrt(1 + 4*Exp/50)) / 2.
        // Dùng vòng lặp an toàn hơn để tránh lỗi làm tròn khi EXP lớn.
        public static int GetLevelFromExp(long totalExp)
        {
            if (totalExp < 0) totalExp = 0;

            int level = 1;
            // Bước nhảy tăng dần, nhưng vì MaxLevel 999 nên loop trực tiếp vẫn nhanh.
            while (level < MaxLevel && totalExp >= MinExpForLevel(level + 1))
                level++;
            return level;
        }

        // Tiến độ trong level hiện tại: (currentExpInLevel, expRange).
        // Trả (exp đã tích trong level, tổng exp cần cho level này).
        public static (long CurrentInLevel, long RangeInLevel) GetProgressInLevel(
            long totalExp, int level)
        {
            if (level < 1) level = 1;
            long start = MinExpForLevel(level);
            long end = MinExpForLevel(level + 1);
            long range = end - start;
            if (range <= 0) return (0, 1);

            long current = totalExp - start;
            if (current < 0) current = 0;
            if (current > range) current = range;
            return (current, range);
        }

        // ---------- Rank title ----------

        public static string GetRankTitle(int level) => level switch
        {
            <= 4 => "Đồng",
            <= 9 => "Bạc",
            <= 19 => "Vàng",
            <= 34 => "Bạch Kim",
            <= 49 => "Kim Cương",
            _ => "Cao Thủ",
        };

        // Nhãn ngắn cho header badge: "Lv 5 · Bạc".
        public static string GetLevelBadge(int level) =>
            $"Lv {level} · {GetRankTitle(level)}";

        // ---------- EXP gain ----------

        // Tính EXP nhận được từ 1 ván đã nộp. Không tin số liệu từ UI.
        // - optimalValue <= 0 → coi như ván lỗi, chỉ nhận BaseExp tối thiểu.
        // - optimalPercent tự tính lại để tránh UI gửi sai.
        public static int CalculateExpGain(
            int score,
            int optimalValue,
            int stars)
        {
            const int BaseExp = 20;
            const int StarBonusPerStar = 30;
            const int PerfectBonus = 50;

            if (optimalValue <= 0) return BaseExp;

            // Clamp score để tránh user gửi giá trị lạ.
            if (score < 0) score = 0;
            if (score > optimalValue) score = optimalValue;

            double percent = (double)score / optimalValue * 100.0;
            int optimalBonus = (int)Math.Floor(percent); // 0..100

            int starBonus = Math.Clamp(stars, 0, 3) * StarBonusPerStar;
            int perfectBonus = percent >= 100.0 ? PerfectBonus : 0;

            return BaseExp + optimalBonus + starBonus + perfectBonus;
        }
    }
}