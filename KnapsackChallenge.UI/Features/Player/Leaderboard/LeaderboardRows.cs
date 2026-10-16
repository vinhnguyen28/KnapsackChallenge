using KnapsackChallenge.Common.DTOs;

namespace KnapsackChallenge.UI.Features.Player
{
    // Helper text dùng chung cho 2 loại bảng.
    // GIỮ NGUYÊN — HomePageView đang dùng Medal() qua ExpBoardRow.RankText.
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

    // =========================================================
    // 1 dòng bảng xếp hạng theo bộ đề.
    // NEW: bổ sung IsTop3 / IsGold / IsSilver / IsBronze / RankDisplay
    //      / Initial / DifficultyKey — phục vụ giao diện mới.
    //      KHÔNG đổi property cũ.
    // =========================================================
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
        public string RankText => LeaderboardText.Medal(Data.Rank);   // giữ nguyên
        public string Username => Data.Username;
        public string SetName => Data.SetName;
        public string DifficultyText => LeaderboardText.Difficulty(Data.Difficulty);
        public string PercentText => $"{Data.OptimalPercent:0.0}%";
        public string ScoreText => $"{Data.BestScore}/{Data.OptimalValue}";
        public string TimeText => Data.TimeText;
        public string AchievedText => Data.AchievedAt.HasValue
            ? Data.AchievedAt.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            : "—";

        // ----- NEW: phục vụ giao diện Bảng xếp hạng v9 -----

        /// <summary>Số hạng thuần (không emoji) — dùng cho cột # khi rank >= 4.</summary>
        public string RankDisplay => Rank.ToString();

        /// <summary>True nếu hạng 1/2/3 — XAML dùng làm cờ tổng (nếu cần).</summary>
        public bool IsTop3 => Rank >= 1 && Rank <= 3;

        public bool IsGold => Rank == 1;
        public bool IsSilver => Rank == 2;
        public bool IsBronze => Rank == 3;

        /// <summary>Chữ cái đầu của username — dùng cho avatar tròn. An toàn với null/rỗng.</summary>
        public string Initial
        {
            get
            {
                var u = Username;
                if (string.IsNullOrWhiteSpace(u)) return "?";
                return u.Trim().Substring(0, 1).ToUpperInvariant();
            }
        }

        /// <summary>Key độ khó gốc ("Easy"/"Medium"/"Hard") — dùng cho DataTrigger đổi màu chip.</summary>
        public string DifficultyKey => (Data.Difficulty ?? "").ToLowerInvariant() switch
        {
            "easy" => "Easy",
            "medium" => "Medium",
            "hard" => "Hard",
            _ => "Unknown",
        };
    }

    // =========================================================
    // 1 dòng bảng xếp hạng theo EXP.
    // NEW: bổ sung IsTop3 / IsGold / IsSilver / IsBronze / RankDisplay / Initial.
    //      KHÔNG đổi property cũ.
    // =========================================================
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
        public string RankText => LeaderboardText.Medal(Data.Rank);   // giữ nguyên — HomePageView bind vào đây
        public string Username => Data.Username;
        public string LevelText => $"Lv {Data.Level}";
        public string RankTitle => Data.RankTitle;
        public string ExpText => Data.TotalExp.ToString("N0");
        public long TotalExp => Data.TotalExp;

        // ----- NEW: phục vụ giao diện Bảng xếp hạng v9 -----

        /// <summary>Số hạng thuần (không emoji) — dùng cho cột # khi rank >= 4.</summary>
        public string RankDisplay => Rank.ToString();

        /// <summary>True nếu hạng 1/2/3.</summary>
        public bool IsTop3 => Rank >= 1 && Rank <= 3;

        public bool IsGold => Rank == 1;
        public bool IsSilver => Rank == 2;
        public bool IsBronze => Rank == 3;

        /// <summary>Chữ cái đầu của username — dùng cho avatar tròn.</summary>
        public string Initial
        {
            get
            {
                var u = Username;
                if (string.IsNullOrWhiteSpace(u)) return "?";
                return u.Trim().Substring(0, 1).ToUpperInvariant();
            }
        }
    }
}