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
    public sealed class SignalRRoomNotifier : IRoomNotifier
    {
        private readonly IHubContext<GameHub> _hub;

        public SignalRRoomNotifier(IHubContext<GameHub> hub)
        {
            _hub = hub;
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

        public Task KickedAsync(int userId, string reason)
            => _hub.Clients.User(UserOf(userId))
                   .SendAsync("Kicked", new { Reason = reason });

        public Task ForceLogoutAsync(int userId, string reason)
            => _hub.Clients.User(UserOf(userId))
                   .SendAsync("ForceLogout", new { Reason = reason });

        public Task RoomClosedAsync(string roomCode, string reason)
            => _hub.Clients.Group(GroupOf(roomCode))
                   .SendAsync("RoomClosed", new { Reason = reason });
    }
}