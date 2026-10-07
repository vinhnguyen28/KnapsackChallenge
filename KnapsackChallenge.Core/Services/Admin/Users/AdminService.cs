using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.Data.Repositories;

namespace KnapsackChallenge.Core.Services.Admin
{
    public class AdminService : IAdminService
    {
        private readonly UserRepository _userRepository;
        private readonly BanLogRepository _banLogRepository;
        private readonly HistoryRepository _historyRepository;

        public AdminService(UserRepository userRepository,
                            BanLogRepository banLogRepository,
                            HistoryRepository historyRepository)
        {
            _userRepository = userRepository;
            _banLogRepository = banLogRepository;
            _historyRepository = historyRepository;
        }

        public List<UserListItemDto> GetAllUsers() => _userRepository.GetAllForAdmin();

        public (int Total, int Online, int Banned, int Today) GetStats()
        {
            var users = _userRepository.GetAllForAdmin();
            int total = users.Count;
            int online = users.Count(u => u.IsOnline);
            int banned = users.Count(u => u.IsBanned);
            int today = _userRepository.CountTodayRegistrations();
            return (total, online, banned, today);
        }

        public (bool Success, string Message) BanUser(int targetUserId, string reason, UserEntity currentAdmin)
        {
            reason = (reason ?? "").Trim();

            if (reason.Length < 5 || reason.Length > 500)
                return (false, "Lý do ban phải từ 5 đến 500 ký tự!");

            if (currentAdmin.Id == targetUserId)
                return (false, "Không thể tự ban chính mình!");

            var target = _userRepository.GetById(targetUserId);
            if (target == null)
                return (false, "Không tìm thấy người chơi.");

            if (string.Equals(target.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                return (false, "Không thể ban tài khoản Admin!");

            if (target.IsBanned)
                return (false, "Tài khoản này đã bị ban.");

            var adminName = currentAdmin.Username ?? "";
            _userRepository.SetBanState(targetUserId, true, reason, adminName);
            _banLogRepository.Add(targetUserId, "Ban", reason, adminName);
            return (true, "Đã ban người chơi.");
        }

        public (bool Success, string Message) UnbanUser(int targetUserId, string? note, UserEntity currentAdmin)
        {
            var target = _userRepository.GetById(targetUserId);
            if (target == null)
                return (false, "Không tìm thấy người chơi.");
            if (!target.IsBanned)
                return (false, "Tài khoản này chưa bị ban.");

            note = (note ?? "").Trim();
            if (note.Length > 500)
                return (false, "Ghi chú tối đa 500 ký tự!");

            var adminName = currentAdmin.Username ?? "";
            _userRepository.SetBanState(targetUserId, false, null, adminName);
            _banLogRepository.Add(targetUserId, "Unban",
                string.IsNullOrEmpty(note) ? null : note, adminName);
            return (true, "Đã bỏ ban người chơi.");
        }

        public PlayerAchievementDto? GetPlayerAchievement(int userId)
        {
            var user = _userRepository.GetById(userId);
            if (user == null) return null;

            var summary = _historyRepository.GetSummary(userId);
            var recent = _historyRepository.GetRecentGames(userId, 20);

            return new PlayerAchievementDto
            {
                UserId = user.Id,
                Username = user.Username ?? "",
                Role = user.Role ?? "",
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                IsBanned = user.IsBanned,
                BanReason = user.BanReason,
                BannedAt = user.BannedAt,
                BannedBy = user.BannedBy,
                TotalGames = summary.TotalGames,
                SubmittedGames = summary.SubmittedGames,
                HighestScore = summary.HighestScore,
                AverageScore = summary.AverageScore,
                TotalScore = summary.TotalScore,
                RecentGames = recent,
            };
        }

        public List<BanLogEntity> GetBanLogs(string? searchUsername = null)
            => _banLogRepository.GetAll(searchUsername);
    }
}