namespace KnapsackChallenge.Core.Services.Player.Multiplayer
{
    // Cổng ghi DB cho RoomManager. Implement ở Server bằng MultiplayerRepository.
    // Mọi method là async để Server có thể await; implementation mặc định
    // bọc Task.Run nếu cần (repository dùng ADO.NET đồng bộ).
    public interface IRoomPersistence
    {
        Task<int> CreateRoomAsync(string roomCode, int hostUserId, int setId, DateTime createdAtUtc);
        Task AddPlayerAsync(int sessionId, int userId, DateTime joinedAtUtc);
        Task RemovePlayerAsync(int sessionId, int userId);
        Task TransferHostAsync(int sessionId, int newHostUserId);
        Task UpdateSetAsync(int sessionId, int setId);
        Task<bool> MarkPlayingAsync(int sessionId, int optimalValue, DateTime startedAtUtc);
        Task SaveSubmissionAsync(int sessionId, int userId,
                                  IReadOnlyList<int> selectedItemIds,
                                  int totalScore, int totalWeight, int timeSpentSeconds);
        Task MarkFinishedAsync(int sessionId, DateTime finishedAtUtc);
        Task MarkKickedAsync(int sessionId, int userId);
        Task MarkBannedAsync(int sessionId, int userId);
        Task DeleteRoomAsync(int sessionId);

        // v8: Rank & EXP — dùng khi finish ván Multiplayer.
        Task<(long TotalExp, int Level)> GetExpAndLevelAsync(int userId);
        Task UpdateExpAndLevelAsync(int userId, long newTotalExp, int newLevel);
    }
}