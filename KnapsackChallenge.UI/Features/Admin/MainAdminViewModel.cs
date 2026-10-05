using System;
using System.Collections.Generic;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using KnapsackChallenge.Core.Factories;
using KnapsackChallenge.Core.Services.Player;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Shared;

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
            _sessionService = ServiceFactory.GetPlayerSessionService();

            LogoutCommand = new RelayCommand<object>(_ => ExecuteLogout());

            // Chọn mặc định mục đầu -> đây cũng là lần navigate đầu tiên.
            SelectedNavItem = NavItems[0];
        }

        private void NavigateTo(string key)
        {
            // Cho VM cũ cơ hội dọn dẹp (dừng DispatcherTimer).
            (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();

            CurrentPage = key switch
            {
                "items" => new ItemManagementViewModel(_dialog),
                "players" => new PlayerManagementViewModel(_adminUser, _dialog),
                "banlogs" => new BanLogViewModel(),
                "sets" => new PlaceholderViewModel("📋", "Quản lý bộ đề / màn chơi",
                                                     "Sẽ được hiện thực ở Giai đoạn 4."),
                _ => new PlaceholderViewModel("👥", "Quản lý người chơi",
                                                     "Không xác định được trang."),
            };
        }

        private void ExecuteLogout()
        {
            if (!_dialog.Confirm("Bạn có chắc muốn đăng xuất?", "Xác nhận đăng xuất"))
                return;

            // Đánh dấu Offline. Nuốt lỗi mạng vì đăng xuất phải luôn thành công phía UI.
            try
            {
                _sessionService.GoOffline(_adminUser.Id);
            }
            catch (SqlException) { }
            catch (InvalidOperationException) { }

            // Dọn dẹp VM con trước khi rời trang.
            (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();

            LogoutRequested?.Invoke();
        }
    }
}