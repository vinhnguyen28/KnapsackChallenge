using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Common.Enums;
using KnapsackChallenge.Core.Services.Player;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Services;
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

        // NEW — phục vụ avatar tròn trong DataGrid tối.
        public string AvatarInitial
        {
            get
            {
                var u = Username;
                if (string.IsNullOrWhiteSpace(u)) return "?";
                return u.Trim().Substring(0, 1).ToUpperInvariant();
            }
        }
    }

    public class MultiplayerRoomViewModel : ViewModelBase, IPageLifecycle
    {
        private readonly UserEntity _user;
        private readonly ISoloGameService _soloService;

        private MultiplayerConnectionService? _connection;
        //private bool _disposed;
        private bool _suppressSetChange;
        private RoomStateDto _state;

        public ObservableCollection<MultiplayerPlayerRow> Players { get; } = new();
        public ObservableCollection<SoloGameSetDto> AvailableSets { get; } = new();

        // Danh sách bộ đề đã lọc theo _setDifficultyFilter (chỉ host dùng).
        public ObservableCollection<SoloGameSetDto> FilteredSets { get; } = new();

        private string _setDifficultyFilter = "";

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
            _soloService = ClientServiceFactory.GetSoloGameService();

            LeaveRoomCommand = new RelayCommand<object>(_ => _ = LeaveRoomAsync());
            StartGameCommand = new RelayCommand<object>(_ => _ = StartGameAsync(), _ => CanStartGame);
            ChangeSetCommand = new RelayCommand<object>(_ => _ = ChangeSetAsync(), _ => IsHost && CanChangeSet && SelectedSet != null);
            RandomSetCommand = new RelayCommand<object>(_ => _ = RandomSetAsync(), _ => IsHost && CanChangeSet && FilteredSets.Count > 0);
            KickPlayerCommand = new RelayCommand<MultiplayerPlayerRow>(row => _ = KickAsync(row), row => CanKick(row));
            CopyCodeCommand = new RelayCommand<object>(_ => CopyCode());

            _ = InitAsync();
        }

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

        // NEW — chuẩn hoá để XAML DataTrigger switch màu ổn định,
        // không phụ thuộc chữ tiếng Việt của StatusText.
        public string DifficultyKey => (_state.Difficulty ?? "").ToLowerInvariant() switch
        {
            "easy" => "Easy",
            "medium" => "Medium",
            "hard" => "Hard",
            _ => "Unknown",
        };

        public string StatusKey => _state.Status switch
        {
            RoomStatus.Waiting => "Waiting",
            RoomStatus.Playing => "Playing",
            RoomStatus.Finished => "Finished",
            _ => "Closed",
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

        public bool IsSetFilterEasy
        {
            get => _setDifficultyFilter == "Easy";
            set { if (value) SetSetDifficultyFilter("Easy"); }
        }
        public bool IsSetFilterMedium
        {
            get => _setDifficultyFilter == "Medium";
            set { if (value) SetSetDifficultyFilter("Medium"); }
        }
        public bool IsSetFilterHard
        {
            get => _setDifficultyFilter == "Hard";
            set { if (value) SetSetDifficultyFilter("Hard"); }
        }

        private void SetSetDifficultyFilter(string value)
        {
            _setDifficultyFilter = value;
            RebuildFilteredSets();
            SelectedSet = FilteredSets.FirstOrDefault(s => s.SetId == _state.SetId)
                       ?? FilteredSets.FirstOrDefault();
            OnPropertyChanged(nameof(IsSetFilterEasy));
            OnPropertyChanged(nameof(IsSetFilterMedium));
            OnPropertyChanged(nameof(IsSetFilterHard));
        }

        private void RebuildFilteredSets()
        {
            FilteredSets.Clear();
            foreach (var s in AvailableSets)
            {
                if (string.Equals(s.Difficulty, _setDifficultyFilter,
                                  StringComparison.OrdinalIgnoreCase))
                    FilteredSets.Add(s);
            }
        }

        private static string NormalizeDifficulty(string? d)
        {
            if (string.Equals(d, "Medium", StringComparison.OrdinalIgnoreCase)) return "Medium";
            if (string.Equals(d, "Hard", StringComparison.OrdinalIgnoreCase)) return "Hard";
            return "Easy";
        }

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

        public ICommand LeaveRoomCommand { get; }
        public ICommand StartGameCommand { get; }
        public ICommand ChangeSetCommand { get; }
        public ICommand RandomSetCommand { get; }
        public ICommand KickPlayerCommand { get; }
        public ICommand CopyCodeCommand { get; }

        public void OnNavigatedFrom()
        {
            //_disposed = true;
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

                if (_setDifficultyFilter == "")
                    _setDifficultyFilter = NormalizeDifficulty(_state.Difficulty);
                RebuildFilteredSets();
                SelectedSet = FilteredSets.FirstOrDefault(s => s.SetId == _state.SetId)
                           ?? FilteredSets.FirstOrDefault();

                OnPropertyChanged(nameof(IsSetFilterEasy));
                OnPropertyChanged(nameof(IsSetFilterMedium));
                OnPropertyChanged(nameof(IsSetFilterHard));

                _suppressSetChange = false;
            }
            catch (Exception ex)
            {
                // Best-effort: vẫn phải ApplyState để phòng hiển thị đúng.
                ErrorMessage = ApiErrorText.From(ex);
            }

            ApplyState(_state);
        }

        private void ApplyState(RoomStateDto state)
        {
            var prevSetId = _state.SetId;

            _state = state;

            Players.Clear();

            foreach (var p in state.Players)
                Players.Add(new MultiplayerPlayerRow(p));

            OnPropertyChanged(nameof(RoomCode));
            OnPropertyChanged(nameof(SetName));
            OnPropertyChanged(nameof(Difficulty));
            OnPropertyChanged(nameof(DifficultyKey));   // NEW
            OnPropertyChanged(nameof(StatusKey));       // NEW
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

            bool serverSetChanged = prevSetId != state.SetId || _setDifficultyFilter == "";
            if (serverSetChanged && !_suppressSetChange)
            {
                _setDifficultyFilter = NormalizeDifficulty(state.Difficulty);
                RebuildFilteredSets();
                _suppressSetChange = true;
                SelectedSet = FilteredSets.FirstOrDefault(s => s.SetId == state.SetId);
                _suppressSetChange = false;
                OnPropertyChanged(nameof(IsSetFilterEasy));
                OnPropertyChanged(nameof(IsSetFilterMedium));
                OnPropertyChanged(nameof(IsSetFilterHard));
            }

            CommandManager.InvalidateRequerySuggested();
        }

        private void OnRoomUpdated(RoomStateDto state)
            => RunOnUi(() => ApplyState(state));

        private void OnGameStarted(GameStartDto start)
        {
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

        private async System.Threading.Tasks.Task RandomSetAsync()
        {
            if (_connection == null || FilteredSets.Count == 0) return;

            var candidates = FilteredSets.Where(s => s.SetId != _state.SetId).ToList();
            if (candidates.Count == 0) candidates = FilteredSets.ToList();
            if (candidates.Count == 0) return;

            SelectedSet = candidates[Random.Shared.Next(candidates.Count)];
            await ChangeSetAsync();
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