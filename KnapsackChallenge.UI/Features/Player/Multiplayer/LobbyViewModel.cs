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
    public class RoomRowViewModel
    {
        public RoomSummaryDto Data { get; }
        public RoomRowViewModel(RoomSummaryDto d) { Data = d; }

        public string RoomCode => Data.RoomCode;
        public string HostUsername => Data.HostUsername;
        public string SetName => Data.SetName;
        public string PlayerCountText => $"{Data.PlayerCount}/{Data.MaxPlayers}";

        public string StatusText => Data.Status switch
        {
            RoomStatus.Waiting => "Đang chờ",
            RoomStatus.Playing => "Đang chơi",
            RoomStatus.Finished => "Kết thúc",
            _ => Data.Status.ToString(),
        };

        public string CreatedAtText => Data.CreatedAtUtc == default
            ? "—"
            : Data.CreatedAtUtc.ToLocalTime().ToString("dd/MM HH:mm");

        public string StartedAtText => Data.StartedAtUtc.HasValue
            ? Data.StartedAtUtc.Value.ToLocalTime().ToString("dd/MM HH:mm")
            : "—";

        public string Difficulty => Data.Difficulty;
        public string DifficultyText => (Data.Difficulty ?? "").ToLowerInvariant() switch
        {
            "easy" => "Dễ",
            "medium" => "Bình thường",
            "hard" => "Khó",
            _ => "—"
        };
        public string TimeLimitText => Data.TimeLimitSeconds <= 0
            ? "Không giới hạn"
            : TimeSpan.FromSeconds(Data.TimeLimitSeconds).ToString(@"mm\:ss");

        public bool CanJoin =>
            Data.Status == RoomStatus.Waiting
            && Data.PlayerCount < Data.MaxPlayers;

        // =========================================================
        // NEW — phục vụ LobbyView tông tối (không đổi logic cũ)
        // =========================================================

        /// <summary>Key chuẩn hoá để XAML DataTrigger switch màu chip/accent bar.</summary>
        public string DifficultyKey => (Data.Difficulty ?? "").ToLowerInvariant() switch
        {
            "easy" => "Easy",
            "medium" => "Medium",
            "hard" => "Hard",
            _ => "Unknown",
        };

        /// <summary>Key chuẩn hoá trạng thái — dùng cho chip "Đang chờ/Đang chơi/Đầy".</summary>
        public string StatusKey
        {
            get
            {
                if (Data.Status == RoomStatus.Playing) return "Playing";
                if (Data.Status == RoomStatus.Finished) return "Finished";
                if (Data.MaxPlayers > 0 && Data.PlayerCount >= Data.MaxPlayers) return "Full";
                return "Waiting";
            }
        }

        /// <summary>Phần trăm đầy (0..100) cho thanh tiến độ mini ở cột Người.</summary>
        public double CapacityPercent =>
            Data.MaxPlayers <= 0 ? 0 : (double)Data.PlayerCount / Data.MaxPlayers * 100.0;

        /// <summary>Chữ cái đầu của chủ phòng (dùng làm avatar tròn). An toàn với null/rỗng.</summary>
        public string AvatarInitial
        {
            get
            {
                var u = HostUsername;
                if (string.IsNullOrWhiteSpace(u)) return "?";
                return u.Trim().Substring(0, 1).ToUpperInvariant();
            }
        }
    }

    public class LobbyViewModel : ViewModelBase, IPageLifecycle
    {
        private readonly UserEntity _user;
        private readonly ISoloGameService _soloService;

        private MultiplayerConnectionService? _connection;
        private bool _disposed;

        // Từ giờ Rooms chỉ chứa kết quả ĐÃ LỌC; _allRooms giữ toàn bộ snapshot mới nhất.
        private List<RoomSummaryDto> _allRooms = new();

        public ObservableCollection<RoomRowViewModel> Rooms { get; } = new();

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotBusy)); }
        }
        public bool IsNotBusy => !_isBusy;

        private string _statusText = "Chưa kết nối";
        public string StatusText
        {
            get => _statusText;
            private set { _statusText = value; OnPropertyChanged(); }
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            private set { _isConnected = value; OnPropertyChanged(); }
        }

        private string _errorMessage = "";
        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
        }
        public bool HasError => !string.IsNullOrEmpty(_errorMessage);

        private string _infoMessage = "";
        public string InfoMessage
        {
            get => _infoMessage;
            set { _infoMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasInfo)); }
        }
        public bool HasInfo => !string.IsNullOrEmpty(_infoMessage);

        // ===== Bộ lọc Lobby =====
        private string _difficultyFilter = ""; // "" = tất cả
        public bool IsFilterAll
        {
            get => _difficultyFilter == "";
            set { if (value) SetDifficultyFilter(""); }
        }
        public bool IsFilterEasy
        {
            get => _difficultyFilter == "Easy";
            set { if (value) SetDifficultyFilter("Easy"); }
        }
        public bool IsFilterMedium
        {
            get => _difficultyFilter == "Medium";
            set { if (value) SetDifficultyFilter("Medium"); }
        }
        public bool IsFilterHard
        {
            get => _difficultyFilter == "Hard";
            set { if (value) SetDifficultyFilter("Hard"); }
        }

        private void SetDifficultyFilter(string value)
        {
            _difficultyFilter = value;
            OnPropertyChanged(nameof(IsFilterAll));
            OnPropertyChanged(nameof(IsFilterEasy));
            OnPropertyChanged(nameof(IsFilterMedium));
            OnPropertyChanged(nameof(IsFilterHard));
            ApplyFilter();
        }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }

        private bool _showPlaying;
        public bool ShowPlaying
        {
            get => _showPlaying;
            set
            {
                if (_showPlaying == value) return;
                _showPlaying = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }

        private RoomRowViewModel? _selectedRoom;
        public RoomRowViewModel? SelectedRoom
        {
            get => _selectedRoom;
            set
            {
                if (ReferenceEquals(_selectedRoom, value)) return;
                _selectedRoom = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand CreateRoomCommand { get; }
        public ICommand JoinRoomCommand { get; }
        public ICommand JoinSelectedCommand { get; }
        public ICommand BackCommand { get; }

        public event Action? BackRequested;
        public event Action<string>? ForceLogoutRequested;

        public event Action<RoomStateDto>? RoomEntered;

        public LobbyViewModel(UserEntity user)
        {
            _user = user;
            _soloService = ClientServiceFactory.GetSoloGameService();

            RefreshCommand = new RelayCommand<object>(_ => _ = RefreshAsync(), _ => IsConnected && !IsBusy);
            CreateRoomCommand = new RelayCommand<object>(_ => _ = ExecuteCreateRoomAsync(), _ => IsConnected && !IsBusy);
            JoinRoomCommand = new RelayCommand<object>(_ => ExecuteJoinRoom(), _ => IsConnected && !IsBusy);
            JoinSelectedCommand = new RelayCommand<object>(
                _ => { if (SelectedRoom != null) _ = JoinRoomAsync(SelectedRoom.RoomCode); },
                _ => IsConnected && !IsBusy && SelectedRoom?.CanJoin == true);
            BackCommand = new RelayCommand<object>(_ => BackRequested?.Invoke());

            _ = InitAsync();
        }

        public void OnNavigatedFrom()
        {
            _disposed = true;

            if (_connection != null)
            {
                _connection.StateChanged -= OnConnectionStateChanged;
                _connection.ForceLogout -= OnForceLogout;
            }
        }

        private async System.Threading.Tasks.Task InitAsync()
        {
            if (!MultiplayerSession.Instance.HasToken)
            {
                RunOnUi(() =>
                {
                    StatusText = "Chưa đăng nhập Server";
                    ErrorMessage =
                        "Không thể kết nối tới Server Multiplayer. " +
                        "Hãy kiểm tra Server đang chạy và thử đăng nhập lại.";
                });
                return;
            }

            try
            {
                _connection = await MultiplayerSession.Instance.EnsureConnectionAsync();

                _connection.StateChanged += OnConnectionStateChanged;
                _connection.ForceLogout += OnForceLogout;

                if (_connection.IsConnected)
                {
                    IsConnected = true;
                    StatusText = "Đã kết nối";
                    await RefreshAsync();
                }
            }
            catch (Exception ex)
            {
                RunOnUi(() =>
                {
                    StatusText = "Mất kết nối";
                    ErrorMessage = $"Không kết nối được Server: {ex.Message}";
                });
            }
        }

        private void ApplyFilter()
        {
            IEnumerable<RoomSummaryDto> q = _allRooms;

            if (!ShowPlaying)
                q = q.Where(r => r.Status == RoomStatus.Waiting);

            if (!string.IsNullOrEmpty(_difficultyFilter))
                q = q.Where(r => string.Equals(r.Difficulty, _difficultyFilter,
                                                StringComparison.OrdinalIgnoreCase));

            var search = (_searchText ?? "").Trim();
            if (search.Length > 0)
            {
                q = q.Where(r =>
                    (r.RoomCode ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (r.HostUsername ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (r.SetName ?? "").Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            var ordered = q
                .OrderBy(r => r.Status == RoomStatus.Waiting ? 0 : 1)
                .ThenByDescending(r => r.CreatedAtUtc)
                .ToList();

            var prevCode = _selectedRoom?.RoomCode;

            Rooms.Clear();
            foreach (var r in ordered)
                Rooms.Add(new RoomRowViewModel(r));

            SelectedRoom = prevCode == null
                ? null
                : Rooms.FirstOrDefault(x => string.Equals(x.RoomCode, prevCode,
                                                          StringComparison.OrdinalIgnoreCase));

            if (_allRooms.Count == 0)
            {
                InfoMessage = "Chưa có phòng nào. Hãy tạo phòng đầu tiên!";
            }
            else
            {
                var msg = $"Hiển thị {Rooms.Count}/{_allRooms.Count} phòng.";
                if (Rooms.Count == 0) msg += " Thử đổi bộ lọc.";
                InfoMessage = msg;
            }
        }

        private async System.Threading.Tasks.Task RefreshAsync()
        {
            if (_connection == null || !_connection.IsConnected || _disposed) return;

            RunOnUi(() => { IsBusy = true; ErrorMessage = ""; InfoMessage = ""; });
            try
            {
                var list = await _connection.ListRoomsAsync();
                if (_disposed) return;

                RunOnUi(() =>
                {
                    _allRooms = list.ToList();
                    ApplyFilter();
                });
            }
            catch (Exception ex)
            {
                RunOnUi(() => ErrorMessage = $"Không tải được danh sách phòng: {ex.Message}");
            }
            finally
            {
                RunOnUi(() => IsBusy = false);
            }
        }

        // Async vì GetAvailableSets đi qua mạng — bọc Task.Run.
        private async System.Threading.Tasks.Task ExecuteCreateRoomAsync()
        {
            if (_connection == null || !_connection.IsConnected) return;

            List<SoloGameSetDto> sets;
            try
            {
                sets = await System.Threading.Tasks.Task.Run(
                    () => _soloService.GetAvailableSets());
            }
            catch (Exception ex)
            {
                ErrorMessage = ApiErrorText.From(ex);
                return;
            }

            if (sets.Count == 0)
            {
                ErrorMessage = "Chưa có bộ đề nào để tạo phòng.";
                return;
            }

            var dlg = new CreateRoomDialog(sets) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true || dlg.Request == null) return;

            _ = CreateRoomAsync(dlg.Request);
        }

        private async System.Threading.Tasks.Task CreateRoomAsync(CreateRoomRequest request)
        {
            if (_connection == null) return;

            RunOnUi(() => { IsBusy = true; ErrorMessage = ""; InfoMessage = ""; });
            try
            {
                var result = await _connection.CreateRoomAsync(request);

                if (result == null)
                {
                    RunOnUi(() => ErrorMessage = "Mất kết nối tới Server.");
                    return;
                }
                if (!result.Success || result.Data == null)
                {
                    RunOnUi(() => ErrorMessage = result.Message);
                    return;
                }

                var state = result.Data.State;
                RunOnUi(() => RoomEntered?.Invoke(state));
            }
            catch (Exception ex)
            {
                RunOnUi(() => ErrorMessage = $"Lỗi tạo phòng: {ex.Message}");
            }
            finally
            {
                RunOnUi(() => IsBusy = false);
            }
        }

        private void ExecuteJoinRoom()
        {
            if (_connection == null || !_connection.IsConnected) return;

            var dlg = new JoinRoomDialog { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;

            _ = JoinRoomAsync(dlg.RoomCode);
        }

        private async System.Threading.Tasks.Task JoinRoomAsync(string roomCode)
        {
            if (_connection == null) return;

            RunOnUi(() => { IsBusy = true; ErrorMessage = ""; InfoMessage = ""; });
            try
            {
                var result = await _connection.JoinRoomAsync(roomCode);

                if (result == null)
                {
                    RunOnUi(() => ErrorMessage = "Mất kết nối tới Server.");
                    return;
                }
                if (!result.Success || result.Data == null)
                {
                    RunOnUi(() => ErrorMessage = result.Message);
                    return;
                }

                var state = result.Data.State;
                RunOnUi(() => RoomEntered?.Invoke(state));
            }
            catch (Exception ex)
            {
                RunOnUi(() => ErrorMessage = $"Lỗi vào phòng: {ex.Message}");
            }
            finally
            {
                RunOnUi(() => IsBusy = false);
            }
        }

        private void OnConnectionStateChanged(MultiplayerConnectionService.ConnectionState s)
        {
            RunOnUi(() =>
            {
                IsConnected = s == MultiplayerConnectionService.ConnectionState.Connected;
                StatusText = s switch
                {
                    MultiplayerConnectionService.ConnectionState.Connecting => "Đang kết nối...",
                    MultiplayerConnectionService.ConnectionState.Connected => "Đã kết nối",
                    MultiplayerConnectionService.ConnectionState.Reconnecting => "Đang thử lại...",
                    _ => "Mất kết nối",
                };

                if (s == MultiplayerConnectionService.ConnectionState.Connected)
                    _ = RefreshAsync();
            });
        }

        private void OnForceLogout(string reason)
        {
            RunOnUi(() =>
            {
                MessageBox.Show(
                    string.IsNullOrWhiteSpace(reason)
                        ? "Tài khoản của bạn đã bị khóa."
                        : reason,
                    "Tài khoản bị khóa",
                    MessageBoxButton.OK, MessageBoxImage.Warning);

                ForceLogoutRequested?.Invoke(reason);
            });
        }

        private static void RunOnUi(Action action)
        {
            var app = Application.Current;
            if (app == null) return;

            var dispatcher = app.Dispatcher;
            if (dispatcher == null) return;

            if (dispatcher.CheckAccess()) action();
            else dispatcher.BeginInvoke(action);
        }
    }
}