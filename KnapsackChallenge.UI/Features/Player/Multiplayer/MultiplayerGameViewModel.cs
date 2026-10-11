using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Shared;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace KnapsackChallenge.UI.Features.Player
{
    // Wrapper 1 dòng trong side panel "Người chơi".
    public class MultiplayerGamePlayerRow
    {
        public RoomPlayerDto Data { get; }
        public MultiplayerGamePlayerRow(RoomPlayerDto d) { Data = d; }

        public int UserId => Data.UserId;
        public string Username => Data.Username;
        public bool IsHost => Data.IsHost;
        public bool IsOnline => Data.IsOnline;
        public bool IsSubmitted => Data.IsSubmitted;
        public int TotalScore => Data.TotalScore;

        public string StatusText => !IsOnline ? "Mất kết nối"
            : IsSubmitted ? "Đã nộp"
            : "Đang chơi";
    }

    // Màn chơi Multiplayer (Phase 4).
    // - Nhận GameStartDto từ Room VM qua constructor.
    // - Không tự gọi StartGame; server đã broadcast GameStarted.
    // - Timer đếm ngược dựa trên EndTimeUtc (server-authoritative), client chỉ hiển thị.
    // - Submit: client chủ động gọi khi user bấm; auto-submit do server lo khi hết giờ.
    public class MultiplayerGameViewModel : ViewModelBase, IPageLifecycle
    {
        private readonly UserEntity _user;
        private readonly GameStartDto _start;
        private readonly DispatcherTimer _timer;

        private MultiplayerConnectionService? _connection;
        private bool _disposed;
        private bool _submitted;
        private int _elapsedSeconds;

        // Vỏ điều hướng subscribe:
        // - GameEndedReceived → mở Result VM.
        // - ForceLeaveToLobby → quay về Lobby (leave / kick / room closed).
        // - ForceLogoutRequested → logout toàn cục (bị ban giữa ván).
        public event Action<FinalRankingDto>? GameEndedReceived;
        public event Action? ForceLeaveToLobby;
        public event Action? ForceLogoutRequested;

        public ObservableCollection<SelectableItem> GameItems { get; } = new();
        public ObservableCollection<MultiplayerGamePlayerRow> Players { get; } = new();

        public MultiplayerGameViewModel(UserEntity user, GameStartDto start)
        {
            _user = user;
            _start = start;

            foreach (var it in start.Items)
                GameItems.Add(new SelectableItem(it));

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += OnTick;

            ToggleItemCommand = new RelayCommand<SelectableItem>(ToggleItem, _ => !HasResult && !_submitted);
            SubmitCommand = new RelayCommand<object>(_ => _ = SubmitAsync(), _ => CanSubmit);
            LeaveCommand = new RelayCommand<object>(_ => _ = LeaveAsync());

            _ = InitAsync();
        }

        // ===== Header info (read-only từ GameStartDto) =====
        public string RoomCode => _start.RoomCode;
        public string SetName => _start.SetName;
        public string Difficulty => _start.Difficulty;
        public int MaxWeight => _start.MaxWeight;
        public int TimeLimitSeconds => _start.TimeLimitSeconds;
        public bool IsCountdown => TimeLimitSeconds > 0;

        public string TimerLabel => IsCountdown ? "Còn lại" : "Thời gian";

        public string TimerText => IsCountdown
            ? TimeSpan.FromSeconds(Math.Max(0, TimeLimitSeconds - _elapsedSeconds)).ToString(@"mm\:ss")
            : TimeSpan.FromSeconds(_elapsedSeconds).ToString(@"mm\:ss");

        public string ProgressText => $"{Players.Count(p => p.IsSubmitted)}/{Players.Count} đã nộp";

        // ===== Chọn vật phẩm =====
        public int TotalSelectedWeight => GameItems.Where(i => i.IsSelected).Sum(i => i.Weight);
        public int TotalSelectedValue => GameItems.Where(i => i.IsSelected).Sum(i => i.Value);
        public int SelectedCount => GameItems.Count(i => i.IsSelected);
        public bool IsOverWeight => MaxWeight > 0 && TotalSelectedWeight > MaxWeight;

        public double CapacityPercent =>
            MaxWeight <= 0 ? 0 : (double)TotalSelectedWeight / MaxWeight * 100.0;

        public string CapacityText =>
            $"{TotalSelectedWeight} / {MaxWeight} khối lượng  •  {TotalSelectedValue} giá trị  •  {SelectedCount} vật phẩm";

        // ===== Trạng thái nộp =====
        private SubmissionResultDto? _myResult;
        public SubmissionResultDto? MyResult
        {
            get => _myResult;
            private set
            {
                _myResult = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasResult));
                OnPropertyChanged(nameof(ResultStarText));
                OnPropertyChanged(nameof(CanSubmit));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool HasResult => _myResult != null;

        public string ResultStarText => _myResult?.Stars switch
        {
            3 => "★★★",
            2 => "★★☆",
            1 => "★☆☆",
            _ => "☆☆☆",
        };

        public bool CanSubmit =>
            !_disposed
            && !_submitted
            && !HasResult
            && GameItems.Count > 0
            && SelectedCount > 0
            && !IsOverWeight;

        // ===== Banners =====
        private string _infoMessage = "";
        public string InfoMessage { get => _infoMessage; set { _infoMessage = value; OnPropertyChanged(); } }

        private string _errorMessage = "";
        public string ErrorMessage { get => _errorMessage; set { _errorMessage = value; OnPropertyChanged(); } }

        // ===== Commands =====
        public ICommand ToggleItemCommand { get; }
        public ICommand SubmitCommand { get; }
        public ICommand LeaveCommand { get; }

        // =========================================================
        // LIFECYCLE
        // =========================================================

        public void OnNavigatedFrom()
        {
            _disposed = true;
            _timer.Stop();
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (_connection == null) return;
            _connection.RoomUpdated -= OnRoomUpdated;
            _connection.PlayerSubmitted -= OnPlayerSubmitted;
            _connection.GameEnded -= OnGameEnded;
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
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Không kết nối được Server: {ex.Message}";
                return;
            }

            // Subscribe TRƯỚC khi load state để không mất event.
            _connection.RoomUpdated += OnRoomUpdated;
            _connection.PlayerSubmitted += OnPlayerSubmitted;
            _connection.GameEnded += OnGameEnded;
            _connection.Kicked += OnKicked;
            _connection.ForceLogout += OnForceLogout;
            _connection.RoomClosed += OnRoomClosed;

            // Lần đầu: kéo state để fill Players (RoomUpdated có thể chưa tới).
            try
            {
                var state = await _connection.GetRoomStateAsync();
                if (state?.Success == true && state.Data != null)
                {
                    Players.Clear();
                    foreach (var p in state.Data.Players)
                        Players.Add(new MultiplayerGamePlayerRow(p));
                }
            }
            catch { /* best-effort */ }

            // Reconnect giữa ván: server có thể đã nhận bài của mình → restore UI.
            try
            {
                var my = await _connection.GetMyResultAsync();
                if (my?.Success == true && my.Data != null)
                {
                    _submitted = true;
                    MyResult = my.Data;
                    InfoMessage = "Bạn đã nộp bài trước đó.";
                }
            }
            catch { /* best-effort */ }

            // Đồng bộ đồng hồ với server (StartTimeUtc + EndTimeUtc).
            if (IsCountdown && _start.EndTimeUtc.HasValue)
            {
                var remaining = (int)Math.Max(0, (_start.EndTimeUtc.Value - DateTime.UtcNow).TotalSeconds);
                _elapsedSeconds = Math.Max(0, TimeLimitSeconds - remaining);
            }
            else if (_start.StartTimeUtc != default)
            {
                _elapsedSeconds = (int)Math.Max(0, (DateTime.UtcNow - _start.StartTimeUtc).TotalSeconds);
            }

            _timer.Start();
            OnPropertyChanged(nameof(TimerText));
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(CanSubmit));
        }

        // =========================================================
        // TIMER
        // =========================================================

        private void OnTick(object? sender, EventArgs e)
        {
            if (_disposed) return;
            _elapsedSeconds++;
            OnPropertyChanged(nameof(TimerText));

            // KHÔNG tự submit khi hết giờ — server có TimeoutToleranceSeconds + auto-submit.
            // Client chỉ đóng băng lựa chọn để tránh user chọn thêm.
            if (IsCountdown && _elapsedSeconds >= TimeLimitSeconds && !HasResult)
            {
                _timer.Stop();
                InfoMessage = "Hết giờ. Đang chờ server tổng kết...";
                OnPropertyChanged(nameof(CanSubmit));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // =========================================================
        // ITEM TOGGLE
        // =========================================================

        private void ToggleItem(SelectableItem? item)
        {
            if (item == null || _submitted || HasResult) return;
            item.IsSelected = !item.IsSelected;
            NotifyGameChanged();
        }

        private void NotifyGameChanged()
        {
            OnPropertyChanged(nameof(TotalSelectedWeight));
            OnPropertyChanged(nameof(TotalSelectedValue));
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(IsOverWeight));
            OnPropertyChanged(nameof(CapacityPercent));
            OnPropertyChanged(nameof(CapacityText));
            OnPropertyChanged(nameof(CanSubmit));
            CommandManager.InvalidateRequerySuggested();
        }

        // =========================================================
        // SUBMIT / LEAVE
        // =========================================================

        private async System.Threading.Tasks.Task SubmitAsync()
        {
            if (_connection == null || !CanSubmit) return;
            ErrorMessage = "";

            try
            {
                var ids = GameItems.Where(i => i.IsSelected).Select(i => i.Id).ToList();
                var result = await _connection.SubmitAsync(ids, _elapsedSeconds);

                if (result == null)
                {
                    ErrorMessage = "Mất kết nối tới Server.";
                    return;
                }
                if (!result.Success || result.Data == null)
                {
                    ErrorMessage = result.Message;
                    return;
                }

                _submitted = true;
                MyResult = result.Data;
                InfoMessage = "Đã nộp bài. Chờ những người chơi khác...";
                OnPropertyChanged(nameof(CanSubmit));
                CommandManager.InvalidateRequerySuggested();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lỗi nộp bài: {ex.Message}";
            }
        }

        private async System.Threading.Tasks.Task LeaveAsync()
        {
            _timer.Stop();
            if (_connection != null)
            {
                try { await _connection.LeaveRoomAsync(); }
                catch { /* điều hướng vẫn phải xảy ra */ }
            }
            ForceLeaveToLobby?.Invoke();
        }

        // =========================================================
        // SIGNALR HANDLERS (background → marshal UI)
        // =========================================================

        private void OnRoomUpdated(RoomStateDto state)
        {
            RunOnUi(() =>
            {
                if (_disposed) return;
                Players.Clear();
                foreach (var p in state.Players)
                    Players.Add(new MultiplayerGamePlayerRow(p));
                OnPropertyChanged(nameof(ProgressText));
            });
        }

        private void OnPlayerSubmitted(RoomPlayerDto p)
        {
            RunOnUi(() =>
            {
                if (_disposed) return;
                var existing = Players.FirstOrDefault(x => x.UserId == p.UserId);
                if (existing != null) Players.Remove(existing);
                Players.Add(new MultiplayerGamePlayerRow(p));
                OnPropertyChanged(nameof(ProgressText));
            });
        }

        private void OnGameEnded(FinalRankingDto ranking)
        {
            RunOnUi(() =>
            {
                if (_disposed) return;
                _timer.Stop();
                GameEndedReceived?.Invoke(ranking);
            });
        }

        private void OnKicked(string reason)
        {
            RunOnUi(() =>
            {
                MessageBox.Show(
                    string.IsNullOrWhiteSpace(reason) ? "Bạn đã bị mời khỏi phòng." : reason,
                    "Bị mời khỏi phòng",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ForceLeaveToLobby?.Invoke();
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
                _timer.Stop();
                ForceLeaveToLobby?.Invoke();
            });
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