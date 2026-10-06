using KnapsackChallenge.Common.DTOs;

namespace KnapsackChallenge.Core.Services.Admin
{
    // Nghiệp vụ trang Thống kê Admin.
    public interface IAdminStatsService
    {
        // mode: null = tất cả, "Solo" hoặc "Multiplayer".
        // fromDate: null = tất cả thời gian.
        AdminStatsOverviewDto GetOverview(string? mode, DateTime? fromDate);
        List<SetStatsDto> GetSetStats(string? mode, DateTime? fromDate);
        List<TopPlayerDto> GetTopPlayers(string? mode, DateTime? fromDate, int top = 10);
    }
}