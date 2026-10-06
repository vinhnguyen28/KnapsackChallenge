namespace KnapsackChallenge.UI.Features.Player
{
    // Lựa chọn bộ đề trong ComboBox filter của trang Xếp hạng.
    // Tách riêng khỏi SoloGameViewModel để dùng chung giữa các trang.
    public class LeaderboardFilterOption
    {
        public int? SetId { get; init; }
        public string DisplayText { get; init; } = "";
    }
}