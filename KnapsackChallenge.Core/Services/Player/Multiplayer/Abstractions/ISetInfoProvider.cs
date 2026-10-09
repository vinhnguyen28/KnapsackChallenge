namespace KnapsackChallenge.Core.Services.Player.Multiplayer
{
    // Thông tin bộ đề + vật phẩm mà RoomManager cần khi start/score.
    public sealed record SetInfo(
        int SetId,
        string SetName,
        string Difficulty,
        int MaxWeight,
        IReadOnlyList<(int Id, int Weight, int Value)> Items);

    // Cổng nạp set info. Implement ở Server bằng GameRepository/SetRepository.
    public interface ISetInfoProvider
    {
        // Trả null nếu set không tồn tại hoặc không có item.
        SetInfo? Load(int setId);
    }
}