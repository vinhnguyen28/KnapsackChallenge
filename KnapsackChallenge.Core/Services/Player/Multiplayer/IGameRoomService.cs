using KnapsackChallenge.Common.DTOs;

namespace KnapsackChallenge.Core.Services.Player.Multiplayer
{
    // Facade cho Server (Hub + Controller) gọi vào. Không dùng ở WPF client.
    public interface IGameRoomService
    {
        // ===== Người chơi =====
        Task<HubResult<CreateRoomResultDto>> CreateRoomAsync(
            int userId, string username, int setId);
        Task<HubResult<JoinRoomResultDto>> JoinRoomAsync(
            int userId, string username, string roomCode);
        Task<HubResult<RoomStateDto>> LeaveRoomAsync(int userId);
        Task<HubResult<RoomStateDto>> ChangeSetAsync(int userId, int setId);
        Task<HubResult<RoomStateDto>> StartGameAsync(int userId);
        Task<HubResult<SubmissionResultDto>> SubmitAsync(int userId, SubmitRequest req);
        Task<HubResult<RoomStateDto>> GetRoomStateAsync(int userId);

        // ===== Reconnect (Bước 5) =====
        Task<bool> MarkDisconnectedAsync(int userId);
        Task<bool> MarkReconnectedAsync(int userId);
        Task<HubResult<GameStartDto>> GetResumeGameDataAsync(int userId);
        Task<HubResult<FinalRankingDto>> GetFinalRankingAsync(int userId);
        Task<HubResult<SubmissionResultDto?>> GetMyResultAsync(int userId);

        // ===== Admin =====
        Task<IReadOnlyList<RoomSummaryDto>> ListRoomsAsync();
        Task<HubResult<RoomStateDto>> GetRoomStateByCodeAsync(string roomCode);
        Task<HubResult<bool>> KickAsync(string roomCode, int targetUserId, string reason);

        // ===== Vòng lặp nền =====
        Task TickAsync(CancellationToken ct = default);
    }
}