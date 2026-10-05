using KnapsackChallenge.UI.Shared;

namespace KnapsackChallenge.UI.Features.Admin
{
    // Chỗ đứng cho các trang chưa hiện thực (Giai đoạn 3/4 sẽ thay).
    public class PlaceholderViewModel : ViewModelBase
    {
        public string Icon { get; }
        public string Title { get; }
        public string Description { get; }

        public PlaceholderViewModel(string icon, string title, string description)
        {
            Icon = icon;
            Title = title;
            Description = description;
        }
    }
}