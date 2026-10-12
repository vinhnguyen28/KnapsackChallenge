using KnapsackChallenge.Common.DTOs;

namespace KnapsackChallenge.Core.Services.Player.Multiplayer
{
    // Thông tin bộ đề + vật phẩm mà RoomManager cần khi start/score.
    // Items là ItemDto có Name để đẩy vào GameStartDto.
    public sealed record SetInfo(
        int SetId,
        string SetName,
        string Difficulty,
        int MaxWeight,
        IReadOnlyList<ItemDto> Items);

    // Cổng nạp set info. Implement ở Server bằng GameRepository/SetRepository.
    public interface ISetInfoProvider
    {
        // Trả null nếu set không tồn tại hoặc không có item.
        SetInfo? Load(int setId);

        // Random 1 bộ đề (có vật phẩm) thuộc độ khó. Trả null nếu mức đó chưa có bộ đề nào.
        SetInfo? LoadRandomByDifficulty(string difficulty);
    }
}