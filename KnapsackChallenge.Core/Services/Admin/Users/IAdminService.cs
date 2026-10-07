using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Data.Entities;

namespace KnapsackChallenge.Core.Services.Admin
{
    public interface IAdminService
    {
        // Danh sách người chơi (không có PasswordHash).
        List<UserListItemDto> GetAllUsers();

        // Thống kê cho hàng thẻ trên cùng màn Quản lý người chơi.
        (int Total, int Online, int Banned, int Today) GetStats();

        // Ban/Unban có kiểm tra nghiệp vụ + ghi BanLogs.
        (bool Success, string Message) BanUser(int targetUserId, string reason, UserEntity currentAdmin);
        (bool Success, string Message) UnbanUser(int targetUserId, string? note, UserEntity currentAdmin);

        // Thành tích 1 người chơi (kèm danh sách ván gần đây).
        PlayerAchievementDto? GetPlayerAchievement(int userId);

        // Lịch sử ban/unban (có thể lọc theo username).
        List<BanLogEntity> GetBanLogs(string? searchUsername = null);
    }
}