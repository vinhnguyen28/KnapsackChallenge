using KnapsackChallenge.UI.Shared;

namespace KnapsackChallenge.UI.Features.Player
{
    // Placeholder cho các mục nav chưa có trang trong khu vực Player.
    // Tách riêng khỏi Admin.PlaceholderViewModel để 2 feature không phụ thuộc chéo.
    public class PlayerPlaceholderViewModel : ViewModelBase
    {
        public string Icon { get; }
        public string Title { get; }
        public string Description { get; }

        public PlayerPlaceholderViewModel(string icon, string title, string description)
        {
            Icon = icon;
            Title = title;
            Description = description;
        }
    }
}