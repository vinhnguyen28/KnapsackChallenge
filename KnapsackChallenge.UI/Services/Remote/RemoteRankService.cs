using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Services.Player;

namespace KnapsackChallenge.UI.Services.Remote
{
    // Bọc REST API Rank/EXP của Server.
    // - GetStatus: server lấy userId từ token, nên tham số userId bị bỏ qua.
    // - AwardExp / CalculateExpGain: chỉ Server được cộng EXP → UI không gọi.
    public sealed class RemoteRankService : IRankService
    {
        public RankStatusDto GetStatus(int userId)
        {
            // userId bị bỏ qua — server lấy từ token.
            return ApiClient.Get<RankStatusDto>("api/player/rank")!;
        }

        public ExpAwardResultDto AwardExp(int userId, int expToAdd)
            => throw new NotSupportedException("Do server thực hiện.");

        public int CalculateExpGain(int score, int optimalValue, int stars)
            => throw new NotSupportedException("Do server thực hiện.");
    }
}