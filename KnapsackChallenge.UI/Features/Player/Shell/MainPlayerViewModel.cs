using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;
using KnapsackChallenge.Common.Constants;
using KnapsackChallenge.Core.Factories;
using KnapsackChallenge.Core.Services.Player;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Shared;

namespace KnapsackChallenge.UI.Features.Player
{
    // Vỏ khu vực Player: giữ heartbeat/ban/logout, điều hướng trang con.
    // KHÔNG còn sidebar - điều hướng chủ yếu qua các nút trong từng trang.
    public class MainPlayerViewModel : ViewModelBase, IPageLifecycle
    {
        private readonly UserEntity _user;
        private readonly IPlayerSessionService _session;
        private readonly IDialogService _dialog;
        private readonly DispatcherTimer _heartbeatTimer;

        private ViewModelBase _currentPage = null!;
        private PlayerNavItem? _selectedNavItem;

        // NavItems chỉ còn dùng nội bộ để xác định trang hiện tại (title ở header).
        public IReadOnlyList<PlayerNavItem> NavItems { get; } = new List<PlayerNavItem>
        {
            new() { Key = "home",        Icon = "🏠", Title = "Trang chủ" },
            new() { Key = "play",        Icon = "🎯", Title = "Chơi"      },
            new() { Key = "leaderboard", Icon = "🏆", Title = "Xếp hạng"  },
            new() { Key = "history",     Icon = "📜", Title = "Lịch sử"   },
        };

        public string PlayerUsername => _user.Username ?? "";
        public string HeartsBadge => "❤ 25/25";
        public string RankBadge => "Chưa xếp hạng";

        private bool _isSettingsOpen;
        public bool IsSettingsOpen
        {
            get => _isSettingsOpen;
            set { _isSettingsOpen = value; OnPropertyChanged(); }
        }

        public ViewModelBase CurrentPage
        {
            get => _currentPage;
            set { _currentPage = value; OnPropertyChanged(); }
        }

        public PlayerNavItem? SelectedNavItem
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
        public ICommand ToggleSettingsCommand { get; }
        public ICommand ShowProfileCommand { get; }
        public ICommand ShowEnterCodeCommand { get; }
        public ICommand ShowSocialCommand { get; }

        public event Action? LogoutRequested;

        public MainPlayerViewModel(UserEntity user, IDialogService? dialog = null)
        {
            _user = user;
            _dialog = dialog ?? new WpfDialogService();
            _session = ServiceFactory.GetPlayerSessionService();

            LogoutCommand = new RelayCommand<object>(_ => ExecuteLogout());
            ToggleSettingsCommand = new RelayCommand<object>(_ => IsSettingsOpen = !IsSettingsOpen);

            ShowProfileCommand = new RelayCommand<object>(_ =>
                ShowPlaceholder("Hồ sơ", "Trang hồ sơ sẽ được bổ sung sau."));
            ShowEnterCodeCommand = new RelayCommand<object>(_ =>
                ShowPlaceholder("Nhập code", "Chức năng nhập code sẽ được bổ sung sau."));
            ShowSocialCommand = new RelayCommand<object>(_ =>
                ShowPlaceholder("Mạng xã hội", "Liên kết mạng xã hội sẽ được bổ sung sau."));

            _heartbeatTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(AppConfig.HeartbeatIntervalSeconds)
            };
            _heartbeatTimer.Tick += OnHeartbeat;
            _heartbeatTimer.Start();

            SelectedNavItem = NavItems[0];
        }

        public void OnNavigatedFrom()
        {
            _heartbeatTimer.Stop();
            (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();
        }

        private void NavigateTo(string key)
        {
            (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();

            CurrentPage = key switch
            {
                "home" => CreateHomePageVm(),
                "play" => CreateSetSelectionVm(),
                "leaderboard" => new LeaderboardViewModel(_user),
                "history" => new HistoryViewModel(_user),
                _ => new PlayerPlaceholderViewModel("🏠", "Trang chủ",
                                                                "Không xác định được trang."),
            };
        }

        // Điều hướng nội bộ về màn theo key, không cần qua SelectedNavItem (tránh bị
        // logic setter coi là "chuyển nav" và bỏ qua nếu trùng key).
        private void NavigateByKey(string key)
        {
            var item = NavItems.FirstOrDefault(n => n.Key == key);
            if (item != null && !ReferenceEquals(_selectedNavItem, item))
            {
                SelectedNavItem = item;
                return;
            }

            // Nếu nav item trùng, vẫn cần refresh page (ví dụ đang ở "play" muốn quay lại "play").
            NavigateTo(key);
        }

        private ViewModelBase CreateHomePageVm()
        {
            var home = new HomePageViewModel(_user);

            home.PlaySoloRequested += () => NavigateByKey("play");

            home.PlayMultiplayerRequested += () =>
            {
                MessageBox.Show("Chế độ nhiều người chơi đang được phát triển.",
                                "Sắp ra mắt",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            };

            // Shortcut từ HomePage (thay cho sidebar đã bỏ).
            home.OpenLeaderboardRequested += () => NavigateByKey("leaderboard");
            home.OpenHistoryRequested += () => NavigateByKey("history");

            return home;
        }

        // Màn chọn mức độ. Chọn mức -> VM random setId -> phát SetChosen.
        private ViewModelBase CreateSetSelectionVm()
        {
            var vm = new SetSelectionViewModel(_user);

            vm.BackRequested += () => NavigateByKey("home");

            vm.SetChosen += setId =>
            {
                // Vẫn giữ nav "play" đang chọn; chỉ đổi CurrentPage sang màn chơi.
                (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();
                CurrentPage = CreateSoloGameVm(setId);
            };

            return vm;
        }

        // Màn chơi với setId đã chọn. Wire 2 event điều hướng.
        private ViewModelBase CreateSoloGameVm(int setId)
        {
            var game = new SoloGameViewModel(_user, setId);

            // Quay lại màn chọn mức độ.
            game.ChangeSetRequested += () =>
            {
                (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();
                CurrentPage = CreateSetSelectionVm();
            };

            // Về thẳng trang chủ.
            game.HomeRequested += () => NavigateByKey("home");

            return game;
        }

        private void ShowPlaceholder(string title, string message)
        {
            IsSettingsOpen = false;
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnHeartbeat(object? sender, EventArgs e)
        {
            try
            {
                var (banned, reason) = _session.CheckStatus(_user.Id);
                if (banned)
                {
                    _heartbeatTimer.Stop();
                    (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();

                    MessageBox.Show(
                        $"Tài khoản của bạn đã bị khóa. Lý do: {reason ?? "(không có)"}\n" +
                        "Bạn sẽ được đưa về màn đăng nhập.",
                        "Tài khoản bị khóa",
                        MessageBoxButton.OK, MessageBoxImage.Warning);

                    LogoutRequested?.Invoke();
                    return;
                }
                _session.Heartbeat(_user.Id);
            }
            catch (SqlException) { }
            catch (InvalidOperationException) { }
        }

        private void ExecuteLogout()
        {
            IsSettingsOpen = false;

            if (!_dialog.Confirm("Bạn có chắc muốn đăng xuất?", "Xác nhận đăng xuất"))
                return;

            _heartbeatTimer.Stop();
            (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();

            try { _session.GoOffline(_user.Id); }
            catch (SqlException) { }
            catch (InvalidOperationException) { }

            LogoutRequested?.Invoke();
        }
    }
}