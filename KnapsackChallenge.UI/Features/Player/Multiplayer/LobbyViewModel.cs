using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Common.Enums;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Shared;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace KnapsackChallenge.UI.Features.Player
{
    // Wrapper 1 dòng phòng trong DataGrid, thêm text tiếng Việt + giờ local.
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
    }

    // Màn Lobby: kết nối SignalR, hiển thị danh sách phòng.
    // Phase 2: chỉ ĐỌC danh sách + hiển thị trạng thái kết nối.
    // Phase 3+: Tạo phòng / Vào phòng / Màn chơi.
    public class LobbyViewModel : ViewModelBase, IPageLifecycle
    {
        private readonly UserEntity _user;

        private MultiplayerConnectionService? _connection;
        private bool _disposed;

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

        public ICommand RefreshCommand { get; }
        public ICommand CreateRoomCommand { get; }
        public ICommand JoinRoomCommand { get; }
        public ICommand BackCommand { get; }

        // Phát khi user bấm "Quay lại" để vỏ điều hướng về Home.
        public event Action? BackRequested;
        // Phát khi Server bắt buộc đăng xuất (ban) — vỏ sẽ kích hoạt logout flow.
        public event Action<string>? ForceLogoutRequested;

        public LobbyViewModel(UserEntity user)
        {
            _user = user;

            RefreshCommand = new RelayCommand<object>(_ => _ = RefreshAsync(), _ => IsConnected && !IsBusy);
            CreateRoomCommand = new RelayCommand<object>(_ => ShowComingSoon());
            JoinRoomCommand = new RelayCommand<object>(_ => ShowComingSoon());
            BackCommand = new RelayCommand<object>(_ => BackRequested?.Invoke());

            _ = InitAsync();
        }

        // =========================================================
        // LIFECYCLE
        // =========================================================

        public void OnNavigatedFrom()
        {
            _disposed = true;
            _ = DisposeConnectionAsync();
        }

        // =========================================================
        // INIT / CONNECT
        // =========================================================

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

            var (token, _, _) = MultiplayerSession.Instance.GetSnapshot();
            if (string.IsNullOrEmpty(token)) return;

            try
            {
                _connection = new MultiplayerConnectionService(
                    MultiplayerServerConfig.ServerUrl, token);

                // Subscribe TRƯỚC khi connect để không bỏ sót event đầu tiên.
                _connection.StateChanged += OnConnectionStateChanged;
                _connection.ForceLogout += OnForceLogout;
                _connection.Kicked += OnKicked;
                _connection.RoomClosed += OnRoomClosed;
                // RoomUpdated/GameStarted/PlayerSubmitted/GameEnded không dùng ở phase 2
                // (Lobby không nằm trong group nào) — subscribe sẵn để phase 3 dùng.

                await _connection.ConnectAsync();

                if (!_disposed)
                    await RefreshAsync();
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

        // =========================================================
        // REFRESH
        // =========================================================

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
                    Rooms.Clear();
                    foreach (var r in list)
                        Rooms.Add(new RoomRowViewModel(r));

                    InfoMessage = list.Count == 0
                        ? "Chưa có phòng nào. Hãy tạo phòng đầu tiên!"
                        : $"Đang có {list.Count} phòng hoạt động.";
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

        // =========================================================
        // SIGNALR EVENT HANDLERS (background thread)
        // =========================================================

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

                // Khi reconnect thành công -> refresh danh sách phòng.
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

        private void OnKicked(string reason)
        {
            RunOnUi(() =>
            {
                MessageBox.Show(
                    string.IsNullOrWhiteSpace(reason) ? "Bạn đã bị mời khỏi phòng." : reason,
                    "Bị mời khỏi phòng",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        private void OnRoomClosed(string reason)
        {
            RunOnUi(() =>
            {
                InfoMessage = string.IsNullOrWhiteSpace(reason) ? "Phòng đã đóng." : reason;
            });
        }

        // =========================================================
        // HELPERS
        // =========================================================

        private void ShowComingSoon()
        {
            MessageBox.Show(
                "Chức năng Tạo phòng / Vào phòng sẽ có ở phiên bản tiếp theo.",
                "Sắp ra mắt",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async System.Threading.Tasks.Task DisposeConnectionAsync()
        {
            if (_connection == null) return;

            try
            {
                _connection.StateChanged -= OnConnectionStateChanged;
                _connection.ForceLogout -= OnForceLogout;
                _connection.Kicked -= OnKicked;
                _connection.RoomClosed -= OnRoomClosed;

                await _connection.DisposeAsync();
            }
            catch { /* ignore */ }
            finally
            {
                _connection = null;
            }
        }

        // Marshal sang UI thread nếu cần. Không crash khi app đang shutdown.
        private static void RunOnUi(Action action)
        {
            var app = Application.Current;
            if (app == null) return;

            var dispatcher = app.Dispatcher;
            if (dispatcher == null) return;

            if (dispatcher.CheckAccess())
                action();
            else
                dispatcher.BeginInvoke(action);
        }
    }
}