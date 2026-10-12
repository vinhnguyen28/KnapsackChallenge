using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Algorithms;
using KnapsackChallenge.Data.Repositories;
using System.Runtime.CompilerServices;

// Cho phép test project gọi Normalize (internal static, thuần logic).
[assembly: InternalsVisibleTo("KnapsackChallenge.Tests")]

namespace KnapsackChallenge.Core.Services.Player
{
    public class LeaderboardService : ILeaderboardService
    {
        private static readonly string[] ValidModes = { "Solo", "Multiplayer" };
        private static readonly string[] ValidDifficulties = { "Easy", "Medium", "Hard" };

        private const int MinTopN = 1;
        private const int MaxTopN = 200;

        private readonly GameRepository _gameRepository;
        private readonly UserRepository _userRepository;

        public LeaderboardService(GameRepository gameRepository, UserRepository userRepository)
        {
            _gameRepository = gameRepository;
            _userRepository = userRepository;
        }

        public List<LeaderboardEntryDto> GetBySet(LeaderboardQuery query)
            => _gameRepository.GetSetLeaderboard(Normalize(query));

        public LeaderboardEntryDto? GetMySetRank(int userId, LeaderboardQuery query)
            => _gameRepository.GetUserSetRank(userId, Normalize(query));

        public List<ExpLeaderboardEntryDto> GetByExp(int topN = 100)
        {
            int n = ClampTopN(topN);
            var list = _userRepository.GetExpLeaderboard(n);

            // Tin DB nhưng vẫn tự tính lại Level từ TotalExp để tự sửa nếu DB lệch.
            foreach (var e in list)
            {
                e.Level = RankRules.GetLevelFromExp(e.TotalExp);
                e.RankTitle = RankRules.GetRankTitle(e.Level);
            }
            return list;
        }

        public ExpLeaderboardEntryDto? GetMyExpRank(int userId)
        {
            var entry = _userRepository.GetUserExpRank(userId);
            if (entry == null) return null;

            entry.Level = RankRules.GetLevelFromExp(entry.TotalExp);
            entry.RankTitle = RankRules.GetRankTitle(entry.Level);
            return entry;
        }

        // Trả BẢN SAO đã chuẩn hoá — KHÔNG sửa object gốc của caller.
        // - Mode/Difficulty: chỉ nhận giá trị hợp lệ, còn lại → null.
        // - SetId <= 0 → null.
        // - TopN clamp 1..200.
        // - SinceUtc giữ nguyên.
        internal static LeaderboardQuery Normalize(LeaderboardQuery? q)
        {
            q ??= new LeaderboardQuery();

            return new LeaderboardQuery
            {
                Mode = NormalizeMode(q.Mode),
                Difficulty = NormalizeDifficulty(q.Difficulty),
                SetId = q.SetId.HasValue && q.SetId.Value > 0 ? q.SetId : null,
                SinceUtc = q.SinceUtc,
                TopN = ClampTopN(q.TopN),
            };
        }

        private static int ClampTopN(int topN)
        {
            if (topN < MinTopN) return MinTopN;
            if (topN > MaxTopN) return MaxTopN;
            return topN;
        }

        private static string? NormalizeMode(string? mode)
        {
            if (string.IsNullOrWhiteSpace(mode)) return null;
            var trimmed = mode.Trim();
            foreach (var v in ValidModes)
                if (string.Equals(v, trimmed, StringComparison.OrdinalIgnoreCase))
                    return v;
            return null;
        }

        private static string? NormalizeDifficulty(string? difficulty)
        {
            if (string.IsNullOrWhiteSpace(difficulty)) return null;
            var trimmed = difficulty.Trim();
            foreach (var v in ValidDifficulties)
                if (string.Equals(v, trimmed, StringComparison.OrdinalIgnoreCase))
                    return v;
            return null;
        }
    }
}