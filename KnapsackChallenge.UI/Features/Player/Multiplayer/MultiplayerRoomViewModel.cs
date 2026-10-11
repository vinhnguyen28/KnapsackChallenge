using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Common.Enums;
using KnapsackChallenge.Core.Factories;
using KnapsackChallenge.Core.Services.Player;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Shared;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace KnapsackChallenge.UI.Features.Player
{
    // Wrapper 1 dòng người chơi trong DataGrid phòng chờ.
    public class MultiplayerPlayerRow
    {
        public RoomPlayerDto Data { get; }
        public MultiplayerPlayerRow(RoomPlayerDto d) { Data = d; }

        public int UserId => Data.UserId;
        public string Username => Data.Username;
        public bool IsHost => Data.IsHost;
        public bool IsOnline => Data.IsOnline;
        public bool IsSubmitted => Data.IsSubmitted;
        public bool IsKicked => Data.IsKicked;

        public string StatusText => Data.IsKicked ? "Đã rời"
            : Data.IsOnline ? "Online" : "Offline";

        public string HostBadge => Data.IsHost ? "HOST" : "";
    }

    public class MultiplayerRoomViewModel : ViewModelBase, IPageLifecycle
    {
        private readonly UserEntity _user;
        private readonly ISoloGameService _soloService;

        private MultiplayerConnectionService? _connection;
        private bool _disposed;
        private bool _suppressSetChange;
        private RoomStateDto _state;

        public ObservableCollection<MultiplayerPlayerRow> Players { get; } = new();
        public ObservableCollection<SoloGameSetDto> AvailableSets { get; } = new();

        // Phase 4: báo cho vỏ điều hướng khi ván bắt đầu.
        public event Action<GameStartDto>? GameStartedReceived;


        // Vỏ điều hướng subscribe để quay về Lobby.
        public event Action? RoomExited;

        // Server đá user do bị ban → vỏ chạy logout flow.
        public event Action? ForceLogoutRequested;

        public MultiplayerRoomViewModel(UserEntity user, RoomStateDto initialState)
        {
            _user = user;
            _state = initialState;
            _soloService = ServiceFactory.GetSoloGameService();

            LeaveRoomCommand = new RelayCommand<object>(_ => _ = LeaveRoomAsync());
            StartGameCommand = new RelayCommand<object>(_ => _ = StartGameAsync(), _ => CanStartGame);
            ChangeSetCommand = new RelayCommand<object>(_ => _ = ChangeSetAsync(),
                _ => IsHost && CanChangeSet && SelectedSet != null);
            KickPlayerCommand = new RelayCommand<MultiplayerPlayerRow>(
                row => _ = KickAsync(row), row => CanKick(row));
            CopyCodeCommand = new RelayCommand<object>(_ => CopyCode());

            _ = InitAsync();
        }

        // =========================================================
        // STATE EXPOSURE
        // =========================================================

        public string RoomCode => _state.RoomCode;
        public string SetName => _state.SetName;
        public string Difficulty => _state.Difficulty;
        public int MaxWeight => _state.MaxWeight;
        public int MaxPlayers => _state.MaxPlayers;
        public int TimeLimitSeconds => _state.TimeLimitSeconds;
        public DateTime CreatedAtUtc => _state.CreatedAtUtc;

        public string StatusText => _state.Status switch
        {
            RoomStatus.Waiting => "Đang chờ",
            RoomStatus.Playing => "Đang chơi",
            RoomStatus.Finished => "Kết thúc",
            _ => "Đã đóng",
        };

        public string TimeLimitText => TimeLimitSeconds <= 0
            ? "Không giới hạn"
            : TimeSpan.FromSeconds(TimeLimitSeconds).ToString(@"mm\:ss");

        public string CreatedAtText => CreatedAtUtc == default
            ? "—"
            : CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

        public bool IsHost => _state.HostUserId == _user.Id;
        public bool IsNotHost => !IsHost;

        public bool CanStartGame =>
            IsHost
            && _state.Status == RoomStatus.Waiting
            && Players.Count(p => p.IsOnline) >= 2;

        public bool CanChangeSet => _state.Status == RoomStatus.Waiting;

        public string HostHintText => IsHost
            ? (CanStartGame ? "Sẵn sàng bắt đầu" : "Cần ít nhất 2 người online")
            : "Chờ chủ phòng bắt đầu";

        // =========================================================
        // SET PICKER
        // =========================================================

        private SoloGameSetDto? _selectedSet;
        public SoloGameSetDto? SelectedSet
        {
            get => _selectedSet;
            set
            {
                if (ReferenceEquals(_selectedSet, value)) return;
                _selectedSet = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // =========================================================
        // BANNERS
        // =========================================================

        private string _infoMessage = "";
        public string InfoMessage
        {
            get => _infoMessage;
            set { _infoMessage = value; OnPropertyChanged(); }
        }

        private string _errorMessage = "";
        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        // =========================================================
        // COMMANDS
        // =========================================================

        public ICommand LeaveRoomCommand { get; }
        public ICommand StartGameCommand { get; }
        public ICommand ChangeSetCommand { get; }
        public ICommand KickPlayerCommand { get; }
        public ICommand CopyCodeCommand { get; }

        // =========================================================
        // LIFECYCLE
        // =========================================================

        public void OnNavigatedFrom()
        {
            _disposed = true;
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (_connection == null) return;
            _connection.RoomUpdated -= OnRoomUpdated;
            _connection.GameStarted -= OnGameStarted;
            _connection.Kicked -= OnKicked;
            _connection.ForceLogout -= OnForceLogout;
            _connection.RoomClosed -= OnRoomClosed;
        }

        // =========================================================
        // INIT
        // =========================================================

        private async System.Threading.Tasks.Task InitAsync()
        {
            try
            {
                _connection = await MultiplayerSession.Instance.EnsureConnectionAsync();

                // Subscribe TRƯỚC khi load state để không mất event nào.
                _connection.RoomUpdated += OnRoomUpdated;
                _connection.GameStarted += OnGameStarted;
                _connection.Kicked += OnKicked;
                _connection.ForceLogout += OnForceLogout;
                _connection.RoomClosed += OnRoomClosed;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Không kết nối được Server: {ex.Message}";
                return;
            }

            // Load danh sách bộ đề cho ComboBox (host mới dùng).
            try
            {
                var sets = await System.Threading.Tasks.Task.Run(() => _soloService.GetAvailableSets());
                _suppressSetChange = true;
                AvailableSets.Clear();
                foreach (var s in sets) AvailableSets.Add(s);
                SelectedSet = AvailableSets.FirstOrDefault(s => s.SetId == _state.SetId);
                _suppressSetChange = false;
            }
            catch { /* best-effort */ }

            ApplyState(_state);
        }

        // =========================================================
        // STATE APPLICATION
        // =========================================================

        private void ApplyState(RoomStateDto state)
        {
            _state = state;

            Players.Clear();
            foreach (var p in state.Players)
                Players.Add(new MultiplayerPlayerRow(p));

            OnPropertyChanged(nameof(RoomCode));
            OnPropertyChanged(nameof(SetName));
            OnPropertyChanged(nameof(Difficulty));
            OnPropertyChanged(nameof(MaxWeight));
            OnPropertyChanged(nameof(MaxPlayers));
            OnPropertyChanged(nameof(TimeLimitSeconds));
            OnPropertyChanged(nameof(CreatedAtUtc));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(TimeLimitText));
            OnPropertyChanged(nameof(CreatedAtText));
            OnPropertyChanged(nameof(IsHost));
            OnPropertyChanged(nameof(IsNotHost));
            OnPropertyChanged(nameof(CanStartGame));
            OnPropertyChanged(nameof(CanChangeSet));
            OnPropertyChanged(nameof(HostHintText));

            // Đồng bộ ComboBox với set hiện tại của server (không trigger command).
            if (!_suppressSetChange)
            {
                _suppressSetChange = true;
                SelectedSet = AvailableSets.FirstOrDefault(s => s.SetId == state.SetId);
                _suppressSetChange = false;
            }

            CommandManager.InvalidateRequerySuggested();
        }

        // =========================================================
        // SIGNALR HANDLERS (background thread → marshal UI)
        // =========================================================

        private void OnRoomUpdated(RoomStateDto state)
            => RunOnUi(() => ApplyState(state));

        private void OnGameStarted(GameStartDto start)
        {
            // KHÔNG hiện MessageBox nữa — fire event để vỏ điều hướng sang màn chơi.
            RunOnUi(() => GameStartedReceived?.Invoke(start));
        }

        private void OnKicked(string reason)
        {
            RunOnUi(() =>
            {
                MessageBox.Show(
                    string.IsNullOrWhiteSpace(reason) ? "Bạn đã bị mời khỏi phòng." : reason,
                    "Bị mời khỏi phòng",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                RoomExited?.Invoke();
            });
        }

        private void OnForceLogout(string reason)
        {
            RunOnUi(() =>
            {
                MessageBox.Show(
                    string.IsNullOrWhiteSpace(reason) ? "Tài khoản đã bị khóa." : reason,
                    "Tài khoản bị khóa",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ForceLogoutRequested?.Invoke();
            });
        }

        private void OnRoomClosed(string reason)
        {
            RunOnUi(() =>
            {
                MessageBox.Show(
                    string.IsNullOrWhiteSpace(reason) ? "Phòng đã đóng." : reason,
                    "Phòng đóng",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                RoomExited?.Invoke();
            });
        }

        // =========================================================
        // COMMAND HANDLERS
        // =========================================================

        private async System.Threading.Tasks.Task LeaveRoomAsync()
        {
            if (_connection == null)
            {
                RoomExited?.Invoke();
                return;
            }

            try { await _connection.LeaveRoomAsync(); }
            catch { /* ignore — điều hướng vẫn phải xảy ra */ }

            RoomExited?.Invoke();
        }

        private async System.Threading.Tasks.Task StartGameAsync()
        {
            if (_connection == null) return;
            ErrorMessage = "";

            try
            {
                var result = await _connection.StartGameAsync();
                if (result == null || !result.Success)
                    RunOnUi(() => ErrorMessage = result?.Message ?? "Không bắt đầu được ván.");
                // Success: server broadcast GameStarted → OnGameStarted xử lý.
            }
            catch (Exception ex)
            {
                RunOnUi(() => ErrorMessage = $"Lỗi: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task ChangeSetAsync()
        {
            if (_connection == null || SelectedSet == null) return;
            ErrorMessage = "";

            try
            {
                var result = await _connection.ChangeSetAsync(SelectedSet.SetId);
                if (result == null || !result.Success)
                    RunOnUi(() => ErrorMessage = result?.Message ?? "Không đổi được bộ đề.");
                else
                    RunOnUi(() => InfoMessage = "Đã đổi bộ đề.");
            }
            catch (Exception ex)
            {
                RunOnUi(() => ErrorMessage = $"Lỗi: {ex.Message}");
            }
        }

        private bool CanKick(MultiplayerPlayerRow? row)
        {
            return IsHost
                && row != null
                && row.UserId != _user.Id
                && !row.IsKicked
                && _state.Status != RoomStatus.Finished
                && _state.Status != RoomStatus.Closed;
        }

        private async System.Threading.Tasks.Task KickAsync(MultiplayerPlayerRow? row)
        {
            if (!CanKick(row) || _connection == null) return;
            var target = row!;

            var dlg = new TextInputDialog(
                "Mời người chơi ra",
                $"Lý do mời {target.Username} ra khỏi phòng (5–500 ký tự):",
                "Nhập lý do...",
                minLength: 5, maxLength: 500)
            { Owner = Application.Current.MainWindow };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var result = await _connection.KickAsync(_state.RoomCode, target.UserId, dlg.InputText);
                if (result == null || !result.Success)
                    RunOnUi(() => ErrorMessage = result?.Message ?? "Không kick được.");
                else
                    RunOnUi(() => InfoMessage = $"Đã mời {target.Username} ra khỏi phòng.");
            }
            catch (Exception ex)
            {
                RunOnUi(() => ErrorMessage = $"Lỗi: {ex.Message}");
            }
        }

        private void CopyCode()
        {
            try
            {
                Clipboard.SetText(RoomCode);
                InfoMessage = "Đã copy mã phòng.";
            }
            catch { /* ignore */ }
        }

        // =========================================================
        // HELPERS
        // =========================================================

        private static void RunOnUi(Action action)
        {
            var app = Application.Current;
            if (app == null) return;
            var d = app.Dispatcher;
            if (d == null) return;

            if (d.CheckAccess()) action();
            else d.BeginInvoke(action);
        }
    }
}