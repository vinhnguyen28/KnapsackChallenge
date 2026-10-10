using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Data.Repositories;

namespace KnapsackChallenge.Core.Services.Player
{
    public class PlayerSessionService : IPlayerSessionService
    {
        private readonly UserRepository _userRepository;
        private readonly IHeartService _hearts;

        public PlayerSessionService(UserRepository userRepository, IHeartService hearts)
        {
            _userRepository = userRepository;
            _hearts = hearts;
        }

        public HeartStatusDto Heartbeat(int userId)
        {
            _userRepository.UpdateLastSeen(userId, true);
            return _hearts.GetStatus(userId);
        }

        public HeartStatusDto GetHeartStatus(int userId) => _hearts.GetStatus(userId);

        public void GoOffline(int userId) => _userRepository.UpdateLastSeen(userId, false);

        public (bool Banned, string? Reason) CheckStatus(int userId)
        {
            var user = _userRepository.GetById(userId);
            return (user?.IsBanned ?? false, user?.BanReason);
        }
    }
}