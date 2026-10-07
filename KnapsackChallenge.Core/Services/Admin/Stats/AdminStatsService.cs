using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Data.Repositories;

namespace KnapsackChallenge.Core.Services.Admin
{
    public class AdminStatsService : IAdminStatsService
    {
        // Chỉ nhận 2 giá trị mode hợp lệ; các giá trị khác coi như null.
        private static readonly string[] ValidModes = { "Solo", "Multiplayer" };

        private readonly StatsRepository _statsRepository;

        public AdminStatsService(StatsRepository statsRepository)
        {
            _statsRepository = statsRepository;
        }

        public AdminStatsOverviewDto GetOverview(string? mode, DateTime? fromDate)
            => _statsRepository.GetOverview(NormalizeMode(mode), fromDate);

        public List<SetStatsDto> GetSetStats(string? mode, DateTime? fromDate)
            => _statsRepository.GetSetStats(NormalizeMode(mode), fromDate);

        public List<TopPlayerDto> GetTopPlayers(string? mode, DateTime? fromDate, int top = 10)
        {
            if (top <= 0) top = 10;
            if (top > 100) top = 100;
            return _statsRepository.GetTopPlayers(NormalizeMode(mode), fromDate, top);
        }

        // Chuẩn hóa mode: chỉ giữ "Solo" hoặc "Multiplayer", còn lại trả null (tất cả).
        private static string? NormalizeMode(string? mode)
        {
            if (string.IsNullOrWhiteSpace(mode)) return null;
            var trimmed = mode.Trim();
            return ValidModes.Contains(trimmed) ? trimmed : null;
        }
    }
}