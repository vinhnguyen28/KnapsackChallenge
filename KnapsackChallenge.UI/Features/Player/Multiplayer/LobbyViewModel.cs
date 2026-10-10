using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Common.Enums;
using KnapsackChallenge.Core.Factories;
using KnapsackChallenge.Core.Services.Player;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Shared;
using Microsoft.Data.SqlClient;
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
    }

    public class LobbyViewModel : ViewModelBase, IPageLifecycle
    {
        private readonly UserEntity _user;
        private readonly ISoloGameService _soloService;

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

        public event Action? BackRequested;
        public event Action<string>? ForceLogoutRequested;

        // Phase 3: báo cho vỏ điều hướng sang MultiplayerRoomView.
        public event Action<RoomStateDto>? RoomEntered;

        public LobbyViewModel(UserEntity user)
        {
            _user = user;
            _soloService = ServiceFactory.GetSoloGameService();

            RefreshCommand = new RelayCommand<object>(_ => _ = RefreshAsync(), _ => IsConnected && !IsBusy);
            CreateRoomCommand = new RelayCommand<object>(_ => ExecuteCreateRoom(), _ => IsConnected && !IsBusy);
            JoinRoomCommand = new RelayCommand<object>(_ => ExecuteJoinRoom(), _ => IsConnected && !IsBusy);
            BackCommand = new RelayCommand<object>(_ => BackRequested?.Invoke());

            _ = InitAsync();
        }

        // =========================================================
        // LIFECYCLE
        // =========================================================

        public void OnNavigatedFrom()
        {
            _disposed = true;

            // Chỉ UNSUBSCRIBE, KHÔNG dispose connection — Room có thể đang dùng chung.
            if (_connection != null)
            {
                _connection.StateChanged -= OnConnectionStateChanged;
                _connection.ForceLogout -= OnForceLogout;
            }
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

            try
            {
                _connection = await MultiplayerSession.Instance.EnsureConnectionAsync();

                // Subscribe TRƯỚC khi refresh.
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
                    foreach (var r in list) Rooms.Add(new RoomRowViewModel(r));

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
        // CREATE / JOIN
        // =========================================================

        private void ExecuteCreateRoom()
        {
            if (_connection == null || !_connection.IsConnected) return;

            List<SoloGameSetDto> sets;
            try
            {
                sets = _soloService.GetAvailableSets();
            }
            catch (SqlException) { ErrorMessage = "Không kết nối được cơ sở dữ liệu!"; return; }

            if (sets.Count == 0)
            {
                ErrorMessage = "Chưa có bộ đề nào để tạo phòng.";
                return;
            }

            var dlg = new CreateRoomDialog(sets) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true) return;

            _ = CreateRoomAsync(dlg.SelectedSetId);
        }

        private async System.Threading.Tasks.Task CreateRoomAsync(int setId)
        {
            if (_connection == null) return;

            RunOnUi(() => { IsBusy = true; ErrorMessage = ""; InfoMessage = ""; });
            try
            {
                var result = await _connection.CreateRoomAsync(setId);

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

        // =========================================================
        // SIGNALR EVENT HANDLERS
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

        // =========================================================
        // HELPERS
        // =========================================================

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