using KnapsackChallenge.Common.DTOs;

namespace KnapsackChallenge.UI.Features.Player
{
    // Helper text dùng chung cho 2 loại bảng.
    public static class LeaderboardText
    {
        public static string Medal(int rank) => rank switch
        {
            1 => "🥇",
            2 => "🥈",
            3 => "🥉",
            _ => rank.ToString()
        };

        public static string Difficulty(string? d) => (d ?? "").ToLowerInvariant() switch
        {
            "easy" => "Dễ",
            "medium" => "Bình thường",
            "hard" => "Khó",
            _ => "—"
        };
    }

    // 1 dòng bảng xếp hạng theo bộ đề.
    public class SetBoardRow
    {
        public LeaderboardEntryDto Data { get; }
        public bool IsMe { get; }

        public SetBoardRow(LeaderboardEntryDto data, int currentUserId)
        {
            Data = data;
            IsMe = data.UserId == currentUserId;
        }

        public int Rank => Data.Rank;
        public string RankText => LeaderboardText.Medal(Data.Rank);
        public string Username => Data.Username;
        public string SetName => Data.SetName;
        public string DifficultyText => LeaderboardText.Difficulty(Data.Difficulty);
        public string PercentText => $"{Data.OptimalPercent:0.0}%";
        public string ScoreText => $"{Data.BestScore}/{Data.OptimalValue}";
        public string TimeText => Data.TimeText;
        public string AchievedText => Data.AchievedAt.HasValue
            ? Data.AchievedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            : "—";
    }

    // 1 dòng bảng xếp hạng theo EXP.
    public class ExpBoardRow
    {
        public ExpLeaderboardEntryDto Data { get; }
        public bool IsMe { get; }

        public ExpBoardRow(ExpLeaderboardEntryDto data, int currentUserId)
        {
            Data = data;
            IsMe = data.UserId == currentUserId;
        }

        public int Rank => Data.Rank;
        public string RankText => LeaderboardText.Medal(Data.Rank);
        public string Username => Data.Username;
        public string LevelText => $"Lv {Data.Level}";
        public string RankTitle => Data.RankTitle;
        public string ExpText => Data.TotalExp.ToString("N0");
        public long TotalExp => Data.TotalExp;
    }
}