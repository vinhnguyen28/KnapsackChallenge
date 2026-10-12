using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Services.Player;
using System.Globalization;

namespace KnapsackChallenge.UI.Services.Remote
{
    // Gọi REST Server cho bảng xếp hạng mới (v9): EXP + theo bộ đề.
    // - Server lấy userId từ claim "sub" → tham số userId trong các method
    //   GetMySetRank / GetMyExpRank bị BỎ QUA.
    // - KHÔNG nuốt ApiException/ServerUnavailableException — để ViewModel quyết định.
    public sealed class RemoteLeaderboardService : ILeaderboardService
    {
        private const int MinTopN = 1;
        private const int MaxTopN = 200;

        public List<LeaderboardEntryDto> GetBySet(LeaderboardQuery query)
        {
            query ??= new LeaderboardQuery();
            var qs = BuildSetQuery(query, includeTopN: true);

            var list = ApiClient.Get<List<LeaderboardEntryDto>>("api/leaderboard/sets" + qs);
            return list ?? new List<LeaderboardEntryDto>();
        }

        public LeaderboardEntryDto? GetMySetRank(int userId, LeaderboardQuery query)
        {
            // userId bị bỏ qua — server lấy từ token.
            query ??= new LeaderboardQuery();
            var qs = BuildSetQuery(query, includeTopN: false);

            // Server trả 204 khi chưa có hạng → ApiClient trả null.
            return ApiClient.Get<LeaderboardEntryDto?>("api/leaderboard/sets/me" + qs);
        }

        public List<ExpLeaderboardEntryDto> GetByExp(int topN = 100)
        {
            int n = ClampTopN(topN);
            var list = ApiClient.Get<List<ExpLeaderboardEntryDto>>(
                $"api/leaderboard/exp?topN={n.ToString(CultureInfo.InvariantCulture)}");
            return list ?? new List<ExpLeaderboardEntryDto>();
        }

        public ExpLeaderboardEntryDto? GetMyExpRank(int userId)
        {
            // userId bị bỏ qua — server lấy từ token.
            return ApiClient.Get<ExpLeaderboardEntryDto?>("api/leaderboard/exp/me");
        }

        // =========================================================
        // Helpers
        // =========================================================

        private static int ClampTopN(int topN)
        {
            if (topN < MinTopN) return MinTopN;
            if (topN > MaxTopN) return MaxTopN;
            return topN;
        }

        private static string BuildSetQuery(LeaderboardQuery query, bool includeTopN)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(query.Mode))
                parts.Add("mode=" + Uri.EscapeDataString(query.Mode));

            if (!string.IsNullOrWhiteSpace(query.Difficulty))
                parts.Add("difficulty=" + Uri.EscapeDataString(query.Difficulty));

            if (query.SetId.HasValue && query.SetId.Value > 0)
                parts.Add("setId=" + query.SetId.Value.ToString(CultureInfo.InvariantCulture));

            if (query.SinceUtc.HasValue)
            {
                var v = query.SinceUtc.Value;
                if (v.Kind != DateTimeKind.Utc) v = v.ToUniversalTime();
                parts.Add("sinceUtc=" + Uri.EscapeDataString(
                    v.ToString("o", CultureInfo.InvariantCulture)));
            }

            if (includeTopN)
                parts.Add("topN=" + ClampTopN(query.TopN).ToString(CultureInfo.InvariantCulture));

            return parts.Count == 0 ? "" : "?" + string.Join("&", parts);
        }
    }
}