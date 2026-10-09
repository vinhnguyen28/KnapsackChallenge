using KnapsackChallenge.Common.DTOs;

namespace KnapsackChallenge.Server.Services.Rooms
{
    // Cổng ghi DB cho RoomManager. Tách interface để test RoomManager
    // bằng mock, không cần SQL Server thật.
    public interface IRoomPersistence
    {
        // Tạo row GameSessions mới (Status='Waiting', Mode='Multiplayer').
        // Trả về SessionId. Đồng thời insert host vào RoomPlayers (IsHost=1).
        Task<int> CreateRoomAsync(string roomCode, int hostUserId, int setId,
                                   DateTime createdAtUtc);

        // Thêm người vào RoomPlayers (IsHost=0). Bỏ qua nếu đã tồn tại.
        Task AddPlayerAsync(int sessionId, int userId, DateTime joinedAtUtc);

        // Xoá 1 người khỏi RoomPlayers.
        Task RemovePlayerAsync(int sessionId, int userId);

        // Chuyển host sang user khác (IsHost=1).
        Task TransferHostAsync(int sessionId, int newHostUserId);

        // Cập nhật SetId khi host đổi bộ đề (chỉ khi Waiting).
        Task UpdateSetAsync(int sessionId, int setId);

        // Đánh dấu ván bắt đầu: Status='Playing', StartedAt=UTC, OptimalValue.
        Task MarkPlayingAsync(int sessionId, int optimalValue, DateTime startedAtUtc);

        // Ghi kết quả 1 người khi nộp bài.
        Task SaveSubmissionAsync(int sessionId, int userId,
                                  IReadOnlyList<int> selectedItemIds,
                                  int totalScore, int totalWeight, int timeSpentSeconds,
                                  bool isAutoSubmitted);

        // Đánh dấu ván kết thúc: Status='Finished', FinishedAt=UTC.
        Task MarkFinishedAsync(int sessionId, DateTime finishedAtUtc);

        // Xoá row GameSessions (khi phòng bị đóng trước khi Playing).
        Task DeleteRoomAsync(int sessionId);

        // Đánh dấu 1 người bị kick (giữ row để lịch sử biết).
        Task MarkKickedAsync(int sessionId, int userId);
    }
}