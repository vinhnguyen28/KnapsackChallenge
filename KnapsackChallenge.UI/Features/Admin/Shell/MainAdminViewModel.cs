using KnapsackChallenge.Core.Services.Player;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Services;
using KnapsackChallenge.UI.Shared;
using System.Windows.Input;

namespace KnapsackChallenge.UI.Features.Admin
{
    public class MainAdminViewModel : ViewModelBase
    {
        private readonly UserEntity _adminUser;
        private readonly IDialogService _dialog;
        private readonly IPlayerSessionService _sessionService;

        private ViewModelBase _currentPage = null!;
        private NavItem? _selectedNavItem;

        public IReadOnlyList<NavItem> NavItems { get; } = new List<NavItem>
        {
            new() { Key = "players", Icon = "👥", Title = "Quản lý người chơi" },
            new() { Key = "items",   Icon = "🎒", Title = "Quản lý vật phẩm"   },
            new() { Key = "sets",    Icon = "📋", Title = "Quản lý bộ đề"      },
            new() { Key = "stats",   Icon = "📊", Title = "Thống kê"           },
            new() { Key = "modes",   Icon = "🎮", Title = "Quản lý chế độ"     },
            new() { Key = "banlogs", Icon = "🚫", Title = "Lịch sử ban"        },
         };

        public string AdminUsername => _adminUser.Username ?? "";

        public ViewModelBase CurrentPage
        {
            get => _currentPage;
            set { _currentPage = value; OnPropertyChanged(); }
        }

        public NavItem? SelectedNavItem
        {
            get => _selectedNavItem;
            set
            {
                if (value == null || ReferenceEquals(_selectedNavItem, value)) return;
                _selectedNavItem = value;
                OnPropertyChanged();
                NavigateTo(value.Key);
            }
        }

        public ICommand LogoutCommand { get; }
        public event Action? LogoutRequested;

        public MainAdminViewModel(UserEntity adminUser, IDialogService? dialog = null)
        {
            _adminUser = adminUser;
            _dialog = dialog ?? new WpfDialogService();
            _sessionService = ClientServiceFactory.GetPlayerSessionService();

            LogoutCommand = new RelayCommand<object>(_ => ExecuteLogout());

            SelectedNavItem = NavItems[0];
        }

        private void NavigateTo(string key)
        {
            (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();

            CurrentPage = key switch
            {
                "items" => new ItemManagementViewModel(_dialog),
                "players" => new PlayerManagementViewModel(_adminUser, _dialog),
                "banlogs" => new BanLogViewModel(),
                "sets" => new SetManagementViewModel(),
                "stats" => new StatsViewModel(),
                "modes" => CreateGameModeVm(),
                _ => new PlaceholderViewModel("👥", "Quản lý người chơi",
                                                     "Không xác định được trang."),
            };
        }

        private GameModeManagementViewModel CreateGameModeVm()
        {
            var vm = new GameModeManagementViewModel();
            vm.SetCurrentAdmin(_adminUser.Username ?? "");
            return vm;
        }

        private void ExecuteLogout()
        {
            if (!_dialog.Confirm("Bạn có chắc muốn đăng xuất?", "Xác nhận đăng xuất"))
                return;

            // Fire-and-forget: không await để không treo UI khi mạng chậm.
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                try { _sessionService.GoOffline(_adminUser.Id); }
                catch (Exception) { /* nuốt — logout phải luôn thành công phía UI */ }
            });

            // Dọn dẹp VM con trước khi rời trang.
            (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();

            LogoutRequested?.Invoke();
        }
    }
}