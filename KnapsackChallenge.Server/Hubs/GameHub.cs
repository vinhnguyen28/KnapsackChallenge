using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Services.Player.Multiplayer;
using KnapsackChallenge.Data.Repositories;
using KnapsackChallenge.Server.Services.Rooms;

namespace KnapsackChallenge.Server.Hubs
{
    // Hub multiplayer. Mọi method yêu cầu JWT.
    // userId luôn lấy từ claim "sub" — KHÔNG nhận từ client.
    [Authorize]
    public sealed class GameHub : Hub
    {
        private readonly IGameRoomService _rooms;
        private readonly UserRepository _users;
        private readonly ILogger<GameHub> _log;

        public GameHub(IGameRoomService rooms,
                       UserRepository users,
                       ILogger<GameHub> log)
        {
            _rooms = rooms;
            _users = users;
            _log = log;
        }

        // =========================================================
        // LIFECYCLE
        // =========================================================

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserIdOrThrow();

            // Nếu user đang ở trong phòng → add lại vào group để nhận sự kiện.
            var state = await _rooms.GetRoomStateAsync(userId);
            if (state.Success && state.Data != null)
            {
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    SignalRRoomNotifier.GroupOf(state.Data.RoomCode));
            }

            _log.LogInformation("Hub connected: user={UserId} conn={Conn}",
                userId, Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _log.LogInformation("Hub disconnected: conn={Conn} ex={Ex}",
                Context.ConnectionId, exception?.Message);
            // Xử lý reconnect window chi tiết ở Bước 5.
            await base.OnDisconnectedAsync(exception);
        }

        // =========================================================
        // CLIENT → SERVER
        // =========================================================

        public Task<HubResult<CreateRoomResultDto>> CreateRoom(CreateRoomRequest req)
        {
            var userId = GetUserIdOrThrow();
            var username = GetUsername(userId);
            return _rooms.CreateRoomAsync(userId, username, req.SetId);
        }

        public Task<HubResult<JoinRoomResultDto>> JoinRoom(string roomCode)
        {
            var userId = GetUserIdOrThrow();
            var username = GetUsername(userId);
            return _rooms.JoinRoomAsync(userId, username, roomCode);
        }

        public async Task<HubResult<RoomStateDto>> LeaveRoom()
        {
            var userId = GetUserIdOrThrow();
            var result = await _rooms.LeaveRoomAsync(userId);

            // Rời group dù thành công hay không (đảm bảo không nhận event cũ).
            if (result.Success && result.Data != null)
            {
                await Groups.RemoveFromGroupAsync(
                    Context.ConnectionId,
                    SignalRRoomNotifier.GroupOf(result.Data.RoomCode));
            }
            return result;
        }

        public Task<HubResult<RoomStateDto>> ChangeSet(int setId)
        {
            var userId = GetUserIdOrThrow();
            return _rooms.ChangeSetAsync(userId, setId);
        }

        public Task<HubResult<RoomStateDto>> StartGame()
        {
            var userId = GetUserIdOrThrow();
            return _rooms.StartGameAsync(userId);
        }

        public Task<HubResult<SubmissionResultDto>> Submit(SubmitRequest req)
        {
            var userId = GetUserIdOrThrow();
            return _rooms.SubmitAsync(userId, req);
        }

        public Task<HubResult<RoomStateDto>> GetRoomState()
        {
            var userId = GetUserIdOrThrow();
            return _rooms.GetRoomStateAsync(userId);
        }

        // =========================================================
        // HELPERS
        // =========================================================

        private int GetUserIdOrThrow()
        {
            var sub = Context.User?.FindFirst("sub")?.Value;
            if (!int.TryParse(sub, out var userId))
                throw new HubException("Unauthorized");
            return userId;
        }

        // Username không có trong JWT (chỉ sub + role). Query 1 lần cho CreateRoom/JoinRoom.
        private string GetUsername(int userId)
            => _users.GetById(userId)?.Username ?? "unknown";
    }
}