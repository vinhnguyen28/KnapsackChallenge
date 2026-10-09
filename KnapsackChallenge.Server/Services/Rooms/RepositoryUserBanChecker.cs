using KnapsackChallenge.Core.Services.Player.Multiplayer;
using KnapsackChallenge.Data.Repositories;

namespace KnapsackChallenge.Server.Services.Rooms
{
    // Adapter mỏng quanh MultiplayerRepository.CheckUserBanned.
    public sealed class RepositoryUserBanChecker : IUserBanChecker
    {
        private readonly MultiplayerRepository _repo;

        public RepositoryUserBanChecker(MultiplayerRepository repo)
        {
            _repo = repo;
        }

        public (bool Exists, bool IsBanned, string? Reason) Check(int userId)
            => _repo.CheckUserBanned(userId);
    }
}