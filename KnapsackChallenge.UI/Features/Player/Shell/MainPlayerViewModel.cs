using KnapsackChallenge.Common.Constants;
using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Services.Player;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Services;
using KnapsackChallenge.UI.Shared;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace KnapsackChallenge.UI.Features.Player
{
    public class MainPlayerViewModel : ViewModelBase, IPageLifecycle
    {
        private readonly UserEntity _user;
        private readonly IPlayerSessionService _session;
        private readonly IRankService _rankService;
        private readonly IDialogService _dialog;
        private readonly DispatcherTimer _heartbeatTimer;

        private ViewModelBase _currentPage = null!;
        private PlayerNavItem? _selectedNavItem;

        // Cờ chống chạy chồng heartbeat (DispatcherTimer có thể tick khi lần
        // trước chưa hoàn tất do mạng chậm).
        private bool _heartbeatBusy;

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

        private string _rankBadge = "Lv 1 · Đồng";
        public string RankBadge
        {
            get => _rankBadge;
            private set { _rankBadge = value; OnPropertyChanged(); }
        }

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
            _session = ClientServiceFactory.GetPlayerSessionService();
            _rankService = ClientServiceFactory.GetRankService();

            LogoutCommand = new RelayCommand<object>(_ => ExecuteLogout());
            ToggleSettingsCommand = new RelayCommand<object>(_ => IsSettingsOpen = !IsSettingsOpen);

            ShowProfileCommand = new RelayCommand<object>(_ =>
                ShowPlaceholder("Hồ sơ", "Trang hồ sơ sẽ được bổ sung sau."));
            ShowEnterCodeCommand = new RelayCommand<object>(_ =>
                ShowPlaceholder("Nhập code", "Chức năng nhập code sẽ được bổ sung sau."));
            ShowSocialCommand = new RelayCommand<object>(_ =>
                ShowPlaceholder("Mạng xã hội", "Liên kết mạng xã hội sẽ được bổ sung sau."));

            // Đọc tim + rank badge lần đầu — chạy async, KHÔNG chặn UI.
            _ = RefreshHeartsOnceAsync();
            _ = LoadRankBadgeAsync();

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
                "leaderboard" => CreateLeaderboardVm(),
                "history" => CreateHistoryVm(),
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

            game.ExpAwarded += award =>
            {
                if (award.ExpGained > 0)
                    RankBadge = $"Lv {award.NewLevel} · {award.RankTitle}";
            };

            game.ChangeSetRequested += () =>
            {
                (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();
                CurrentPage = CreateSetSelectionVm();
            };

            game.HomeRequested += () => NavigateByKey("home");

            return game;
        }

        private ViewModelBase CreateLeaderboardVm()
        {
            var vm = new LeaderboardViewModel(_user);
            vm.BackRequested += () => NavigateByKey("home");
            return vm;
        }

        private ViewModelBase CreateHistoryVm()
        {
            var vm = new HistoryViewModel(_user);
            vm.BackRequested += () => NavigateByKey("home");
            return vm;
        }

        private ViewModelBase CreateLobbyVm()
        {
            var lobby = new LobbyViewModel(_user);

            lobby.BackRequested += () => NavigateByKey("home");

            lobby.RoomEntered += state =>
            {
                (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();
                CurrentPage = CreateRoomVm(state);
            };

            lobby.ForceLogoutRequested += async _ =>
            {
                _heartbeatTimer.Stop();
                (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();
                await MultiplayerSession.Instance.DisposeConnectionAsync();
                MultiplayerSession.Instance.Clear();
                LogoutRequested?.Invoke();
            };

            return lobby;
        }

        private ViewModelBase CreateRoomVm(RoomStateDto initialState)
        {
            var room = new MultiplayerRoomViewModel(_user, initialState);

            room.RoomExited += () =>
            {
                (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();
                CurrentPage = CreateLobbyVm();
            };

            room.ForceLogoutRequested += () =>
            {
                _heartbeatTimer.Stop();
                (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();
                _ = MultiplayerSession.Instance.DisposeConnectionAsync();
                MultiplayerSession.Instance.Clear();
                LogoutRequested?.Invoke();
            };

            // Phase 4: ván bắt đầu → chuyển sang màn chơi.
            room.GameStartedReceived += start =>
            {
                (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();
                CurrentPage = CreateMultiplayerGameVm(start);
            };

            return room;
        }

        private ViewModelBase CreateMultiplayerGameVm(GameStartDto start)
        {
            var game = new MultiplayerGameViewModel(_user, start);

            // Tất cả nộp hoặc hết giờ → server broadcast GameEnded → mở BXH.
            game.GameEndedReceived += ranking =>
            {
                (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();
                CurrentPage = CreateMultiplayerResultVm(ranking);
            };

            // Rời phòng / bị kick / phòng đóng → về Lobby.
            game.ForceLeaveToLobby += () =>
            {
                (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();
                CurrentPage = CreateLobbyVm();
            };

            // Bị ban giữa ván → logout toàn cục.
            game.ForceLogoutRequested += () =>
            {
                _heartbeatTimer.Stop();
                (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();
                _ = MultiplayerSession.Instance.DisposeConnectionAsync();
                MultiplayerSession.Instance.Clear();
                LogoutRequested?.Invoke();
            };

            return game;
        }

        private ViewModelBase CreateMultiplayerResultVm(FinalRankingDto ranking)
        {
            var result = new MultiplayerResultViewModel(_user, ranking);

            // Quay về Lobby mới tinh (refresh sạch).
            result.BackToLobbyRequested += () =>
            {
                (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();
                CurrentPage = CreateLobbyVm();
            };

            // Quay thẳng về Trang chủ.
            result.BackToHomeRequested += () =>
            {
                (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();
                NavigateByKey("home");
            };

            return result;
        }

        private void ShowPlaceholder(string title, string message)
        {
            IsSettingsOpen = false;
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // =========================================================
        // FIRST-LOAD (ASYNC) — không chặn UI
        // =========================================================

        private async System.Threading.Tasks.Task RefreshHeartsOnceAsync()
        {
            try
            {
                var status = await System.Threading.Tasks.Task.Run(
                    () => _session.GetHeartStatus(_user.Id));
                HeartsBadge = status.BadgeText;
            }
            catch (Exception)
            {
                // Nuốt im lặng — badge giữ giá trị mặc định "❤ --/--".
            }
        }

        private async System.Threading.Tasks.Task LoadRankBadgeAsync()
        {
            try
            {
                var rank = await System.Threading.Tasks.Task.Run(
                    () => _rankService.GetStatus(_user.Id));
                RankBadge = rank.LevelBadge;
            }
            catch (Exception)
            {
                // Nuốt im lặng — badge giữ giá trị mặc định "Lv 1 · Đồng".
            }
        }

        // =========================================================
        // HEARTBEAT — async void + cờ chống chạy chồng
        // =========================================================

        private async void OnHeartbeat(object? sender, EventArgs e)
        {
            if (_heartbeatBusy) return;
            _heartbeatBusy = true;
            try
            {
                bool banned;
                string? reason;

                try
                {
                    var result = await System.Threading.Tasks.Task.Run(
                        () => _session.CheckStatus(_user.Id));
                    banned = result.Banned;
                    reason = result.Reason;
                }
                catch (Exception)
                {
                    // Lỗi mạng/API khác: nuốt im lặng, timer tiếp tục chạy.
                    return;
                }

                // Đã đăng xuất ở luồng khác → không áp kết quả.
                if (!_heartbeatTimer.IsEnabled) return;

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

                HeartStatusDto? heartStatus = null;
                try
                {
                    heartStatus = await System.Threading.Tasks.Task.Run(
                        () => _session.Heartbeat(_user.Id));
                }
                catch (Exception)
                {
                    // Lỗi mạng khi heartbeat: giữ badge cũ.
                }

                if (!_heartbeatTimer.IsEnabled) return;

                if (heartStatus != null)
                    HeartsBadge = heartStatus.BadgeText;
            }
            finally
            {
                _heartbeatBusy = false;
            }
        }

        // =========================================================
        // LOGOUT
        // =========================================================

        private void ExecuteLogout()
        {
            IsSettingsOpen = false;

            if (!_dialog.Confirm("Bạn có chắc muốn đăng xuất?", "Xác nhận đăng xuất"))
                return;

            _heartbeatTimer.Stop();
            (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();

            // Fire-and-forget: không await để không treo UI khi mạng chậm.
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                try { _session.GoOffline(_user.Id); }
                catch (Exception) { /* nuốt — logout phải luôn thành công phía UI */ }
            });

            // Phase 3: đóng kết nối SignalR nếu còn.
            _ = MultiplayerSession.Instance.DisposeConnectionAsync();
            MultiplayerSession.Instance.Clear();

            LogoutRequested?.Invoke();
        }
    }
}