namespace KnapsackChallenge.Core.Services.Player.Multiplayer
{
    // Cổng kiểm tra trạng thái ban. Implement ở Server (dùng MultiplayerRepository
    // hoặc UserRepository). Tách interface để RoomManager test được.
    public interface IUserBanChecker
    {
        // Trả về (Exists, IsBanned, Reason).
        (bool Exists, bool IsBanned, string? Reason) Check(int userId);
    }
}