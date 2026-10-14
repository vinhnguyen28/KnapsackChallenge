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
        private readonly DispatcherTimer _heartTickTimer;

        private ViewModelBase _currentPage = null!;
        private PlayerNavItem? _selectedNavItem;

        // Cờ chống chạy chồng heartbeat (DispatcherTimer có thể tick khi lần
        // trước chưa hoàn tất do mạng chậm).
        private bool _heartbeatBusy;

        // Trạng thái tim hiện tại + cờ chống gọi dồn khi countdown về 0.
        private HeartStatusDto? _heart;
        private bool _heartRefreshBusy;
        private DateTime _lastHeartRefreshUtc = DateTime.MinValue;

        public IReadOnlyList<PlayerNavItem> NavItems { get; } = new List<PlayerNavItem>
        {
            new() { Key = "home",        Icon = "🏠", Title = "Trang chủ" },
            new() { Key = "play",        Icon = "🎯", Title = "Chơi"      },
            new() { Key = "multiplayer", Icon = "🎮", Title = "Phòng chơi"     },
            new() { Key = "leaderboard", Icon = "🏆", Title = "Xếp hạng"  },
            new() { Key = "history",     Icon = "📜", Title = "Lịch sử"   },
        };

        public string PlayerUsername => _user.Username ?? "";

        private string _rankBadge = "Lv 1 · Đồng";
        public string RankBadge
        {
            get => _rankBadge;
            private set { _rankBadge = value; OnPropertyChanged(); }
        }

        // =========================================================
        // HEART BADGE — các property chỉ-đọc dẫn xuất từ _heart.
        // =========================================================

        public string HeartCountText =>
            _heart == null ? "--/--" : $"{_heart.Current}/{_heart.Max}";

        public bool HeartIsEmpty => _heart?.IsEmpty == true;

        public bool HeartIsLow =>
            _heart != null
            && !_heart.IsEmpty
            && _heart.Current <= Math.Max(1, _heart.Max / 5);

        public string HeartTooltipTitle => "Tim = lượt chơi";

        public string HeartTooltipDescription =>
            "Mỗi ván Solo tốn 1 tim. Hết tim bạn phải chờ hồi mới chơi tiếp được.";

        public string HeartRefillRuleText =>
            _heart == null ? "" : $"Cứ {_heart.RefillIntervalMinutes} phút hồi 1 tim.";

        public string HeartCountdownText
        {
            get
            {
                if (_heart == null) return "Đang tải...";
                if (_heart.IsFull) return "Tim đã đầy";
                if (_heart.IsEmpty)
                    return $"Hết tim — tim kế tiếp sau {_heart.CountdownText}";
                return $"Tim kế tiếp sau {_heart.CountdownText}";
            }
        }

        // Gán trạng thái tim mới + thông báo mọi property phụ thuộc.
        private void ApplyHeart(HeartStatusDto status)
        {
            _heart = status;
            OnPropertyChanged(nameof(HeartCountText));
            OnPropertyChanged(nameof(HeartIsEmpty));
            OnPropertyChanged(nameof(HeartIsLow));
            OnPropertyChanged(nameof(HeartRefillRuleText));
            OnPropertyChanged(nameof(HeartCountdownText));
        }

        private bool _isSettingsOpen;
        public bool IsSettingsOpen
        {
            get => _isSettingsOpen;
            set { _isSettingsOpen = value; OnPropertyChanged(); }
        }

        // Header chi hien tren Home va man chon muc do Solo.
        // Cac trang khac (Solo game, Lobby, Room, Multiplayer game, Result,
        // Leaderboard, History) an header.
        public bool IsHeaderVisible =>
            CurrentPage is HomePageViewModel || CurrentPage is SetSelectionViewModel;

        public ViewModelBase CurrentPage
        {
            get => _currentPage;
            set
            {
                _currentPage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsHeaderVisible));

                // Khi header an thi popup cai dat phai dong lai.
                if (!IsHeaderVisible)
                    IsSettingsOpen = false;
            }
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

            // Timer riêng đếm ngược hồi tim — 1 giây/lần.
            _heartTickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _heartTickTimer.Tick += OnHeartTick;
            _heartTickTimer.Start();

            SelectedNavItem = NavItems[0];
        }

        public void OnNavigatedFrom()
        {
            StopTimers();
            (CurrentPage as IPageLifecycle)?.OnNavigatedFrom();
        }

        // Dừng cả 2 timer (heartbeat + heart tick). Dùng chung ở mọi
        // điểm thoát: OnNavigatedFrom, ExecuteLogout, bị ban, ForceLogout.
        private void StopTimers()
        {
            _heartbeatTimer.Stop();
            _heartTickTimer.Stop();
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
            game.HeartsChanged += ApplyHeart;

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
                StopTimers();
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
                StopTimers();
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
                StopTimers();
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
                ApplyHeart(status);
            }
            catch (Exception)
            {
                // Nuốt im lặng — HeartCountText hiển thị "--/--".
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
        // HEART COUNTDOWN TICK — 1 giây/lần
        // - Cập nhật text đếm ngược realtime trên tooltip.
        // - Khi countdown về 0 (và chưa full) thì gọi server lấy lại tim,
        //   có cờ chống chạy chồng và cooldown 5s để chống lệch đồng hồ.
        // =========================================================
        private void OnHeartTick(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(HeartCountdownText));

            if (_heart == null || _heart.IsFull) return;
            if (_heart.SecondsUntilNextRefill > 0) return;
            if (_heartRefreshBusy) return;

            // Chống gọi dồn khi đồng hồ client lệch server.
            if ((DateTime.UtcNow - _lastHeartRefreshUtc).TotalSeconds < 5) return;

            _ = RefreshHeartFromServerAsync();
        }

        private async System.Threading.Tasks.Task RefreshHeartFromServerAsync()
        {
            if (_heartRefreshBusy) return;
            _heartRefreshBusy = true;
            _lastHeartRefreshUtc = DateTime.UtcNow;
            try
            {
                var status = await System.Threading.Tasks.Task.Run(
                    () => _session.GetHeartStatus(_user.Id));
                if (status != null)
                    ApplyHeart(status);
            }
            catch (Exception)
            {
                // Nuốt im lặng — lần tick sau sẽ thử lại.
            }
            finally
            {
                _heartRefreshBusy = false;
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
                    StopTimers();
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
                    ApplyHeart(heartStatus);
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

            StopTimers();
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