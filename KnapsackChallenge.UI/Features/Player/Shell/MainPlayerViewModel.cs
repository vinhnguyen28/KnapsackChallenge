using KnapsackChallenge.Common.Constants;
using KnapsackChallenge.Core.Factories;
using KnapsackChallenge.Core.Services.Player;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Shared;
using Microsoft.Data.SqlClient;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace KnapsackChallenge.UI.Features.Player
{
    public class MainPlayerViewModel : ViewModelBase, IPageLifecycle
    {
        private readonly UserEntity _user;
        private readonly IPlayerSessionService _session;
        private readonly IDialogService _dialog;
        private readonly DispatcherTimer _heartbeatTimer;

        private ViewModelBase _currentPage = null!;
        private PlayerNavItem? _selectedNavItem;

        public IReadOnlyList<PlayerNavItem> NavItems { get; } = new List<PlayerNavItem>
        {
            new() { Key = "home",        Icon = "🏠", Title = "Trang chủ" },
            new() { Key = "play",        Icon = "🎯", Title = "Chơi"      },
            new() { Key = "multiplayer", Icon = "🎮", Title = "Phòng chơi"     },
            new() { Key = "leaderboard", Icon = "🏆", Title = "Xếp hạng"  },
            new() { Key = "history",     Icon = "📜", Title = "Lịch sử"   },
        };

        public string PlayerUsername => _user.Username ?? "";

        // v7: HeartsBadge động — cập nhật mỗi heartbeat 30s.
        private string _heartsBadge = "❤ --/--";
        public string HeartsBadge
        {
            get => _heartsBadge;
            private set { _heartsBadge = value; OnPropertyChanged(); }
        }

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

            // Đọc tim lần đầu ngay khi vào vỏ Player.
            RefreshHeartsOnce();

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
                "multiplayer" => CreateLobbyVm(),
                "leaderboard" => new LeaderboardViewModel(_user),
                "history" => new HistoryViewModel(_user),
                _ => new PlayerPlaceholderViewModel("🏠", "Trang chủ",
                                                    "Không xác định được trang."),
            };
        }

        private void NavigateByKey(string key)
        {
            var item = NavItems.FirstOrDefault(n => n.Key == key);
            if (item != null && !ReferenceEquals(_selectedNavItem, item))
            {
                SelectedNavItem = item;
                return;
            }
            NavigateTo(key);
        }

        private ViewModelBase CreateHomePageVm()
        {
            var home = new HomePageViewModel(_user);

            home.PlaySoloRequested += () => NavigateByKey("play");

            //home.PlayMultiplayerRequested += () =>
            //{
            //    MessageBox.Show("Chế độ nhiều người chơi đang được phát triển.",
            //                    "Sắp ra mắt",
            //                    MessageBoxButton.OK, MessageBoxImage.Information);
            //};

            home.PlayMultiplayerRequested += () => NavigateByKey("multiplayer");

            home.OpenLeaderboardRequested += () => NavigateByKey("leaderboard");
            home.OpenHistoryRequested += () => NavigateByKey("history");

            return home;
        }

        private ViewModelBase CreateSetSelectionVm()
        {
            var vm = new SetSelectionViewModel(_user);

            vm.BackRequested += () => NavigateByKey("home");

            vm.SetChosen += setId =>
            {
                (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();
                CurrentPage = CreateSoloGameVm(setId);
            };

            return vm;
        }

        private ViewModelBase CreateSoloGameVm(int setId)
        {
            var game = new SoloGameViewModel(_user, setId);

            // Sau khi start ván, cập nhật lại badge tim ngay (không chờ 30s).
            game.HeartsChanged += status => HeartsBadge = status.BadgeText;

            game.ChangeSetRequested += () =>
            {
                (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();
                CurrentPage = CreateSetSelectionVm();
            };

            game.HomeRequested += () => NavigateByKey("home");

            return game;
        }

        private ViewModelBase CreateLobbyVm()
        {
            var lobby = new LobbyViewModel(_user);

            lobby.BackRequested += () => NavigateByKey("home");

            // Server đá user (ban) -> chạy full logout flow giống heartbeat phát hiện ban.
            lobby.ForceLogoutRequested += _ =>
            {
                _heartbeatTimer.Stop();
                (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();
                LogoutRequested?.Invoke();
            };

            return lobby;
        }

        private void ShowPlaceholder(string title, string message)
        {
            IsSettingsOpen = false;
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RefreshHeartsOnce()
        {
            try
            {
                var status = _session.GetHeartStatus(_user.Id);
                HeartsBadge = status.BadgeText;
            }
            catch (SqlException) { }
            catch (InvalidOperationException) { }
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

                    MultiplayerSession.Instance.Clear();

                    LogoutRequested?.Invoke();
                    return;
                }

                // Heartbeat giờ trả về HeartStatusDto — cập nhật badge luôn.
                var heartStatus = _session.Heartbeat(_user.Id);
                HeartsBadge = heartStatus.BadgeText;
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

            MultiplayerSession.Instance.Clear();

            LogoutRequested?.Invoke();
        }
    }
}