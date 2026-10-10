using KnapsackChallenge.Common.Constants;
using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Common.Enums;
using KnapsackChallenge.Core.Services.Player.Multiplayer;
using KnapsackChallenge.Data.Repositories;
using KnapsackChallenge.Server.Services.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

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
            int userId;
            try { userId = GetUserIdOrThrow(); }
            catch { Context.Abort(); return; }

            // (8) Ban check ngay tại handshake: nếu bị ban, đá về + đóng WS.
            //     UserRepository.GetById đã có sẵn, không cần thêm DI.
            var user = _users.GetById(userId);
            if (user == null || user.IsBanned)
            {
                var reason = user?.BanReason ?? "(không có)";
                try
                {
                    await Clients.Caller.SendAsync("ForceLogout",
                        new { Reason = string.Format(Messages.ForceLogoutFmt, reason) });
                }
                catch { /* client có thể đã ngắt */ }
                Context.Abort();
                return;
            }

            // Đăng ký connection vào tracker (chưa biết phòng).
            _tracker.Track(userId, Context.ConnectionId, roomCode: null);

            // Nếu user đang ở trong phòng → add lại vào group + báo RoomManager
            // là đã reconnect, rồi gửi lại context tuỳ trạng thái phòng.
            try
            {
                var state = await _rooms.GetRoomStateAsync(userId);
                if (state.Success && state.Data != null)
                {
                    var code = state.Data.RoomCode;
                    await Groups.AddToGroupAsync(Context.ConnectionId,
                        SignalRRoomNotifier.GroupOf(code));
                    _tracker.SetRoom(Context.ConnectionId, code);

                    await _rooms.MarkReconnectedAsync(userId);

                    if (state.Data.Status == RoomStatus.Playing)
                    {
                        // Gửi lại GameStarted cho CHÍNH connection vừa nối.
                        // Client dùng EndTimeUtc để khôi phục đồng hồ đếm ngược.
                        var gameData = await _rooms.GetResumeGameDataAsync(userId);
                        if (gameData.Success && gameData.Data != null)
                            await Clients.Caller.SendAsync("GameStarted", gameData.Data);
                    }
                    else if (state.Data.Status == RoomStatus.Finished)
                    {
                        // Grace 60s — gửi lại bảng xếp hạng cuối.
                        var final = await _rooms.GetFinalRankingAsync(userId);
                        if (final.Success && final.Data != null)
                            await Clients.Caller.SendAsync("GameEnded", final.Data);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "OnConnectedAsync reconnect handling failed for user={UserId}", userId);
            }

            _log.LogInformation("Hub connected: user={UserId} conn={Conn}",
                userId, Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Lấy userId từ claim trước khi Untrack — sau Untrack vẫn còn, nhưng
            // để rõ ràng ta tách bước.
            int? userId = null;
            var sub = Context.User?.FindFirst("sub")?.Value;
            if (int.TryParse(sub, out var parsed)) userId = parsed;

            _tracker.Untrack(Context.ConnectionId);

            // (1) Chỉ coi là OFFLINE khi đây là connection CUỐI CÙNG của user.
            if (userId.HasValue && _tracker.GetConnections(userId.Value).Count == 0)
            {
                try
                {
                    await _rooms.MarkDisconnectedAsync(userId.Value);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "MarkDisconnectedAsync failed for user={UserId}", userId.Value);
                }
            }

            _log.LogInformation("Hub disconnected: user={UserId} conn={Conn} ex={Ex}",
                userId, Context.ConnectionId, exception?.Message);
            await base.OnDisconnectedAsync(exception);
        }

        // =========================================================
        // CLIENT → SERVER
        // =========================================================

        public async Task<HubResult<CreateRoomResultDto>> CreateRoom(CreateRoomRequest? req)
        {
            var userId = GetUserIdOrThrow();

            var err = GameHubValidation.ValidateCreateRoom(req);
            if (err != null)
                return HubResult<CreateRoomResultDto>.Fail(ErrorCodes.RequestInvalid, err);

            var user = _users.GetById(userId);
            if (user == null || string.IsNullOrWhiteSpace(user.Username))
                return HubResult<CreateRoomResultDto>.Fail(
                    ErrorCodes.AuthAccountBanned,
                    string.Format(Messages.AuthAccountBannedFmt, "(không có)"));

            var result = await _rooms.CreateRoomAsync(userId, user.Username!, req!.SetId);

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

        public async Task<HubResult<JoinRoomResultDto>> JoinRoom(string? roomCode)
        {
            var userId = GetUserIdOrThrow();

            var err = GameHubValidation.ValidateJoinRoom(roomCode);
            if (err != null)
                return HubResult<JoinRoomResultDto>.Fail(ErrorCodes.RequestInvalid, err);

            var user = _users.GetById(userId);
            if (user == null || string.IsNullOrWhiteSpace(user.Username))
                return HubResult<JoinRoomResultDto>.Fail(
                    ErrorCodes.AuthAccountBanned,
                    string.Format(Messages.AuthAccountBannedFmt, "(không có)"));

            var result = await _rooms.JoinRoomAsync(userId, user.Username!, roomCode!);

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

            var err = GameHubValidation.ValidateChangeSet(setId);
            if (err != null)
                return Task.FromResult(HubResult<RoomStateDto>.Fail(
                    ErrorCodes.RequestInvalid, err));

            return _rooms.ChangeSetAsync(userId, setId);
        }

        public Task<HubResult<RoomStateDto>> StartGame()
        {
            var userId = GetUserIdOrThrow();
            return _rooms.StartGameAsync(userId);
        }

        public Task<HubResult<SubmissionResultDto>> Submit(SubmitRequest? req)
        {
            var userId = GetUserIdOrThrow();

            var err = GameHubValidation.ValidateSubmit(req);
            if (err != null)
                return Task.FromResult(HubResult<SubmissionResultDto>.Fail(
                    ErrorCodes.RequestInvalid, err));

            return _rooms.SubmitAsync(userId, req!);
        }

        // (5) GetRoomState thêm ban check — user bị ban không đọc được state phòng.
        public async Task<HubResult<RoomStateDto>> GetRoomState()
        {
            var userId = GetUserIdOrThrow();

            var user = _users.GetById(userId);
            if (user == null || user.IsBanned)
            {
                var reason = user?.BanReason ?? "(không có)";
                return HubResult<RoomStateDto>.Fail(
                    ErrorCodes.AuthAccountBanned,
                    string.Format(Messages.AuthAccountBannedFmt, reason));
            }

            return await _rooms.GetRoomStateAsync(userId);
        }

        // (7) Client gọi sau khi reconnect (hoặc bất kỳ lúc nào đang trong phòng)
        //     để lấy lại kết quả đã nộp. Data=null nghĩa là chưa nộp.
        public async Task<HubResult<SubmissionResultDto?>> GetMyResult()
        {
            var userId = GetUserIdOrThrow();

            var user = _users.GetById(userId);
            if (user == null || user.IsBanned)
            {
                var reason = user?.BanReason ?? "(không có)";
                return HubResult<SubmissionResultDto?>.Fail(
                    ErrorCodes.AuthAccountBanned,
                    string.Format(Messages.AuthAccountBannedFmt, reason));
            }

            return await _rooms.GetMyResultAsync(userId);
        }

        // =========================================================
        // LOBBY — danh sách phòng đang hoạt động
        // =========================================================

        // Trả danh sách phòng Waiting/Playing. Bất kỳ user đã đăng nhập đều gọi được.
        // Trả list rỗng (không throw) khi user không hợp lệ để tránh làm vỡ Lobby UI.
        public async Task<IReadOnlyList<RoomSummaryDto>> ListRooms()
        {
            var userId = GetUserIdOrThrow();

            var user = _users.GetById(userId);
            if (user == null || user.IsBanned)
                return Array.Empty<RoomSummaryDto>();

            return await _rooms.ListRoomsAsync();
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