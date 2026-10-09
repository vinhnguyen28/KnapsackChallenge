using KnapsackChallenge.Common.DTOs;

namespace KnapsackChallenge.Server.Services.Rooms
{
    // Singleton in-memory. Pure state machine. Body viết ở bước sau.
    public class RoomManager
    {
        // ---------- Hub methods (client -> server) ----------
        public Task<HubResult<CreateRoomResultDto>> CreateRoomAsync(int userId, CreateRoomRequest req);
        public Task<HubResult<JoinRoomResultDto>> JoinRoomAsync(int userId, string roomCode);
        public Task<HubResult<RoomStateDto>> LeaveRoomAsync(int userId);
        public Task<HubResult<RoomStateDto>> ChangeSetAsync(int userId, int setId);
        public Task<HubResult<RoomStateDto>> StartGameAsync(int userId);
        public Task<HubResult<SubmissionResultDto>> SubmitAsync(int userId, SubmitRequest req);
        public Task<HubResult<RoomStateDto>> GetRoomStateAsync(int userId);

        // ---------- Admin / system ----------
        public Task<IReadOnlyList<RoomSummaryDto>> ListRoomsAsync();
        public Task<HubResult<RoomStateDto>> GetRoomStateByCodeAsync(string roomCode);
        public Task<HubResult<bool>> KickAsync(string roomCode, int targetUserId, string reason);

        // ---------- Vòng lặp nền gọi định kỳ ----------
        public Task TickAsync(DateTime nowUtc);
    }
}