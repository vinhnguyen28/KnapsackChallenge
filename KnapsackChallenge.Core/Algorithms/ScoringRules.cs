namespace KnapsackChallenge.Core.Algorithms
{
    // Quy tắc chấm điểm dùng chung cho Solo và Multiplayer.
    // Ngưỡng giống hệt SoloGameService: 100% → 3, >=90 → 2, >=70 → 1, còn lại 0.
    public static class ScoringRules
    {
        public static int CalculateStars(double optimalPercent) =>
            optimalPercent >= 100.0 ? 3
          : optimalPercent >= 90.0 ? 2
          : optimalPercent >= 70.0 ? 1
          : 0;
    }
}