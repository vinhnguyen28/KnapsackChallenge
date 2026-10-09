using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using KnapsackChallenge.Common.Constants;
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
        private readonly ConnectionTracker _tracker;
        private readonly ILogger<GameHub> _log;

        public GameHub(IGameRoomService rooms,
                       UserRepository users,
                       ConnectionTracker tracker,
                       ILogger<GameHub> log)
        {
            _rooms = rooms;
            _users = users;
            _tracker = tracker;
            _log = log;
        }

        // =========================================================
        // LIFECYCLE
        // =========================================================

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserIdOrThrow();

            // Đăng ký connection vào tracker (chưa biết phòng).
            _tracker.Track(userId, Context.ConnectionId, roomCode: null);

            // Nếu user đang ở trong phòng → add lại vào group để nhận sự kiện.
            var state = await _rooms.GetRoomStateAsync(userId);
            if (state.Success && state.Data != null)
            {
                var code = state.Data.RoomCode;
                await Groups.AddToGroupAsync(Context.ConnectionId,
                    SignalRRoomNotifier.GroupOf(code));
                _tracker.SetRoom(Context.ConnectionId, code);
            }

            _log.LogInformation("Hub connected: user={UserId} conn={Conn}",
                userId, Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _tracker.Untrack(Context.ConnectionId);
            _log.LogInformation("Hub disconnected: conn={Conn} ex={Ex}",
                Context.ConnectionId, exception?.Message);
            await base.OnDisconnectedAsync(exception);
        }

        // =========================================================
        // CLIENT → SERVER
        // =========================================================

        public async Task<HubResult<CreateRoomResultDto>> CreateRoom(CreateRoomRequest req)
        {
            var userId = GetUserIdOrThrow();

            var user = _users.GetById(userId);
            if (user == null || string.IsNullOrWhiteSpace(user.Username))
                return HubResult<CreateRoomResultDto>.Fail(
                    ErrorCodes.AuthAccountBanned,
                    string.Format(Messages.AuthAccountBannedFmt, "(không có)"));

            var result = await _rooms.CreateRoomAsync(userId, user.Username!, req.SetId);

            // Chỉ add group khi tạo phòng THÀNH CÔNG — nếu Fail, client không ở trong group nào.
            if (result.Success && result.Data != null)
            {
                var code = result.Data.RoomCode;
                await Groups.AddToGroupAsync(Context.ConnectionId,
                    SignalRRoomNotifier.GroupOf(code));
                _tracker.SetRoom(Context.ConnectionId, code);
            }

            return result;
        }

        public async Task<HubResult<JoinRoomResultDto>> JoinRoom(string roomCode)
        {
            var userId = GetUserIdOrThrow();

            var user = _users.GetById(userId);
            if (user == null || string.IsNullOrWhiteSpace(user.Username))
                return HubResult<JoinRoomResultDto>.Fail(
                    ErrorCodes.AuthAccountBanned,
                    string.Format(Messages.AuthAccountBannedFmt, "(không có)"));

            var result = await _rooms.JoinRoomAsync(userId, user.Username!, roomCode ?? "");

            if (result.Success && result.Data?.State != null)
            {
                var code = result.Data.State.RoomCode;
                await Groups.AddToGroupAsync(Context.ConnectionId,
                    SignalRRoomNotifier.GroupOf(code));
                _tracker.SetRoom(Context.ConnectionId, code);
            }

            return result;
        }

        public async Task<HubResult<RoomStateDto>> LeaveRoom()
        {
            var userId = GetUserIdOrThrow();

            // Lấy roomCode TỪ TRACKER trước khi rời để chắc chắn gỡ được group
            // ngay cả khi _rooms.LeaveRoomAsync thất bại (vd: user không ở phòng nào).
            var roomCodeFromTracker = _tracker.GetRoom(Context.ConnectionId);

            var result = await _rooms.LeaveRoomAsync(userId);

            var roomCode = roomCodeFromTracker;
            if (string.IsNullOrEmpty(roomCode) && result.Success && result.Data != null)
                roomCode = result.Data.RoomCode;

            if (!string.IsNullOrEmpty(roomCode))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId,
                    SignalRRoomNotifier.GroupOf(roomCode));
            }

            // Xoá liên kết connection <-> room, vẫn giữ user <-> connection.
            _tracker.ClearRoom(Context.ConnectionId);

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
    }
}