using KnapsackChallenge.Data.Repositories;

namespace KnapsackChallenge.Core.Services.Player
{
    public class PlayerSessionService : IPlayerSessionService
    {
        private readonly UserRepository _userRepository;

        public PlayerSessionService(UserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public void Heartbeat(int userId) => _userRepository.UpdateLastSeen(userId, true);

        public void GoOffline(int userId) => _userRepository.UpdateLastSeen(userId, false);

        public (bool Banned, string? Reason) CheckStatus(int userId)
        {
            var user = _userRepository.GetById(userId);
            return (user?.IsBanned ?? false, user?.BanReason);
        }
    }
}