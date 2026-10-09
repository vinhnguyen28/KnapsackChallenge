using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Services.Player.Multiplayer;
using KnapsackChallenge.Server.Hubs;

namespace KnapsackChallenge.Server.Services.Rooms
{
    // Đẩy sự kiện qua SignalR.
    // - Sự kiện phòng → group "room:{code}".
    // - Sự kiện cá nhân → Clients.User(userId) (khớp ClaimUserIdProvider).
    // - Khi kick/ban/room closed: gỡ TẤT CẢ connection liên quan khỏi group
    //   (dùng ConnectionTracker) để người chơi không còn nhận state phòng.
    public sealed class SignalRRoomNotifier : IRoomNotifier
    {
        private readonly IHubContext<GameHub> _hub;
        private readonly ConnectionTracker _tracker;

        public SignalRRoomNotifier(IHubContext<GameHub> hub, ConnectionTracker tracker)
        {
            _hub = hub;
            _tracker = tracker;
        }

        // Group name có tiền tố để không đụng group nội bộ SignalR.
        public static string GroupOf(string roomCode) => "room:" + roomCode;

        private static string UserOf(int userId) =>
            userId.ToString(CultureInfo.InvariantCulture);

        public Task RoomUpdatedAsync(string roomCode, RoomStateDto state)
            => _hub.Clients.Group(GroupOf(roomCode)).SendAsync("RoomUpdated", state);

        public Task GameStartedAsync(string roomCode, GameStartDto start)
            => _hub.Clients.Group(GroupOf(roomCode)).SendAsync("GameStarted", start);

        public Task PlayerSubmittedAsync(string roomCode, RoomPlayerDto player)
            => _hub.Clients.Group(GroupOf(roomCode)).SendAsync("PlayerSubmitted", player);

        public Task GameEndedAsync(string roomCode, FinalRankingDto ranking)
            => _hub.Clients.Group(GroupOf(roomCode)).SendAsync("GameEnded", ranking);

        // Khi user bị kick: gỡ tất cả connection của user khỏi group phòng hiện tại
        // rồi mới gửi Kicked qua kênh user (không phụ thuộc group).
        public async Task KickedAsync(int userId, string reason)
        {
            await RemoveUserFromAllGroupsAsync(userId);
            await _hub.Clients.User(UserOf(userId))
                .SendAsync("Kicked", new { Reason = reason });
        }

        public async Task ForceLogoutAsync(int userId, string reason)
        {
            await RemoveUserFromAllGroupsAsync(userId);
            await _hub.Clients.User(UserOf(userId))
                .SendAsync("ForceLogout", new { Reason = reason });
        }

        // Khi phòng đóng: broadcast trước (cho những connection còn lại),
        // sau đó mới gỡ group để không nhận event nào nữa.
        public async Task RoomClosedAsync(string roomCode, string reason)
        {
            await _hub.Clients.Group(GroupOf(roomCode))
                .SendAsync("RoomClosed", new { Reason = reason });

            await RemoveRoomFromAllGroupsAsync(roomCode);
        }

        // ----- helpers -----

        private async Task RemoveUserFromAllGroupsAsync(int userId)
        {
            foreach (var connId in _tracker.GetConnections(userId))
            {
                var room = _tracker.GetRoom(connId);
                if (string.IsNullOrEmpty(room)) continue;

                await _hub.Groups.RemoveFromGroupAsync(connId, GroupOf(room));
                _tracker.ClearRoom(connId);
            }
        }

        private async Task RemoveRoomFromAllGroupsAsync(string roomCode)
        {
            foreach (var connId in _tracker.GetConnectionsInRoom(roomCode))
            {
                await _hub.Groups.RemoveFromGroupAsync(connId, GroupOf(roomCode));
                _tracker.ClearRoom(connId);
            }
        }
    }
}