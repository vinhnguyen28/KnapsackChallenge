using KnapsackChallenge.Core.Services.Player.Multiplayer;
using KnapsackChallenge.Data.Repositories;

namespace KnapsackChallenge.Server.Services.Rooms
{
    // Bọc MultiplayerRepository (ADO.NET đồng bộ) thành async.
    // Không giữ state — Singleton an toàn.
    public sealed class SqlRoomPersistence : IRoomPersistence
    {
        private readonly MultiplayerRepository _repo;

        public SqlRoomPersistence(MultiplayerRepository repo)
        {
            _repo = repo;
        }

        public Task<int> CreateRoomAsync(string roomCode, int hostUserId, int setId, DateTime createdAtUtc)
            => Task.Run(() => _repo.CreateRoom(roomCode, hostUserId, setId, createdAtUtc));

        public Task AddPlayerAsync(int sessionId, int userId, DateTime joinedAtUtc)
            => Task.Run(() => _repo.AddPlayer(sessionId, userId, joinedAtUtc));

        public Task RemovePlayerAsync(int sessionId, int userId)
            => Task.Run(() => _repo.RemovePlayer(sessionId, userId));

        public Task TransferHostAsync(int sessionId, int newHostUserId)
            => Task.Run(() => _repo.TransferHost(sessionId, newHostUserId));

        public Task UpdateSetAsync(int sessionId, int setId)
            => Task.Run(() => _repo.UpdateSet(sessionId, setId));

        public Task<bool> MarkPlayingAsync(int sessionId, int optimalValue, DateTime startedAtUtc)
            => Task.Run(() => _repo.MarkPlaying(sessionId, optimalValue, startedAtUtc));

        public Task SaveSubmissionAsync(int sessionId, int userId,
                                         IReadOnlyList<int> selectedItemIds,
                                         int totalScore, int totalWeight, int timeSpentSeconds)
            => Task.Run(() => _repo.SaveSubmission(
                sessionId, userId, selectedItemIds, totalScore, totalWeight, timeSpentSeconds));

        public Task MarkFinishedAsync(int sessionId, DateTime finishedAtUtc)
            => Task.Run(() => _repo.MarkFinished(sessionId, finishedAtUtc));

        public Task MarkKickedAsync(int sessionId, int userId)
            => Task.Run(() => _repo.MarkKicked(sessionId, userId));

        public Task MarkBannedAsync(int sessionId, int userId)
            => Task.Run(() => _repo.MarkBanned(sessionId, userId));

        public Task DeleteRoomAsync(int sessionId)
            => Task.Run(() => _repo.DeleteRoom(sessionId));

        public Task<(long TotalExp, int Level)> GetExpAndLevelAsync(int userId)
    => Task.Run(() => _repo.GetExpAndLevel(userId));

        public Task UpdateExpAndLevelAsync(int userId, long newTotalExp, int newLevel)
            => Task.Run(() => _repo.UpdateExpAndLevel(userId, newTotalExp, newLevel));
    }
}