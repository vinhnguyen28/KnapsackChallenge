using KnapsackChallenge.Common.DTOs;

namespace KnapsackChallenge.Core.Services.Player
{
    // Nghiệp vụ bảng xếp hạng mới (v9): theo bộ đề + theo EXP.
    public interface ILeaderboardService
    {
        // Bảng xếp hạng theo bộ đề/độ khó (xếp theo % tối ưu DESC, thời gian ASC).
        List<LeaderboardEntryDto> GetBySet(LeaderboardQuery query);

        // Hạng của 1 người trong cùng phạm vi lọc. Trả null nếu chưa có ván hợp lệ.
        LeaderboardEntryDto? GetMySetRank(int userId, LeaderboardQuery query);

        // Bảng xếp hạng theo EXP/Level toàn hệ thống.
        List<ExpLeaderboardEntryDto> GetByExp(int topN = 100);

        // Hạng EXP của 1 người. Trả null nếu không tồn tại / bị ban / chưa có EXP.
        ExpLeaderboardEntryDto? GetMyExpRank(int userId);
    }
}