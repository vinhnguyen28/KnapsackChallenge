namespace KnapsackChallenge.UI.Features.Player
{
    // Mục trong menu trái của MainPlayerView.
    // Tạo riêng (không tái sử dụng NavItem của Admin) để 2 feature không phụ thuộc chéo nhau.
    public class PlayerNavItem
    {
        public string Key { get; init; } = "";
        public string Icon { get; init; } = "";   // Emoji hiển thị, đồng bộ phong cách Admin
        public string Title { get; init; } = "";
    }
}