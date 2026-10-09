using KnapsackChallenge.Common.DTOs;

namespace KnapsackChallenge.Server.Services.Rooms
{
    // Cổng đẩy sự kiện SignalR. Tách interface để RoomManager test được.
    public interface IRoomNotifier
    {
        Task RoomUpdatedAsync(string roomCode, RoomStateDto state);
        Task GameStartedAsync(string roomCode, GameStartDto start);
        Task PlayerSubmittedAsync(string roomCode, RoomPlayerDto player);
        Task GameEndedAsync(string roomCode, FinalRankingDto ranking);

        Task KickedAsync(int userId, string reason);
        Task ForceLogoutAsync(int userId, string reason);
        Task RoomClosedAsync(string roomCode, string reason);
    }
}