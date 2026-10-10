using KnapsackChallenge.Common.DTOs;
using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;

namespace KnapsackChallenge.UI.Features.Player
{
    // Bọc HubConnection: chỉ 1 nơi duy nhất biết về SignalR.
    // - Dispose khi rời Lobby (phase 2 — có thể đổi thành session-level ở phase 3).
    // - Mọi event callback chạy trên BACKGROUND THREAD. Caller phải marshal sang UI thread.
    public sealed class MultiplayerConnectionService : IAsyncDisposable
    {
        public enum ConnectionState { Disconnected, Connecting, Connected, Reconnecting }

        private readonly string _serverUrl;
        private readonly string _token;
        private readonly HubConnection _connection;

        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        // Sự kiện cho UI subscribe. Mọi invoke đến từ background thread.
        public event Action<ConnectionState>? StateChanged;
        public event Action<RoomStateDto>? RoomUpdated;
        public event Action<GameStartDto>? GameStarted;
        public event Action<RoomPlayerDto>? PlayerSubmitted;
        public event Action<FinalRankingDto>? GameEnded;
        public event Action<string>? Kicked;
        public event Action<string>? ForceLogout;
        public event Action<string>? RoomClosed;

        public MultiplayerConnectionService(string serverUrl, string token)
        {
            _serverUrl = serverUrl;
            _token = token;

            _connection = new HubConnectionBuilder()
                .WithUrl($"{serverUrl}/hubs/game", options =>
                {
                    // JWT qua query string access_token (đã cấu hình trong Program.cs).
                    options.AccessTokenProvider = () => Task.FromResult<string?>(_token);
                })
                .WithAutomaticReconnect(new[]
                {
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(20),
                })
                .Build();

            // Đăng ký nhận event từ server.
            _connection.On<RoomStateDto>("RoomUpdated", d => RoomUpdated?.Invoke(d));
            _connection.On<GameStartDto>("GameStarted", d => GameStarted?.Invoke(d));
            _connection.On<RoomPlayerDto>("PlayerSubmitted", d => PlayerSubmitted?.Invoke(d));
            _connection.On<FinalRankingDto>("GameEnded", d => GameEnded?.Invoke(d));

            // Server đẩy các event này dưới dạng { Reason: "..." }.
            _connection.On<JsonElement>("Kicked", d => Kicked?.Invoke(ReadReason(d)));
            _connection.On<JsonElement>("ForceLogout", d => ForceLogout?.Invoke(ReadReason(d)));
            _connection.On<JsonElement>("RoomClosed", d => RoomClosed?.Invoke(ReadReason(d)));

            _connection.Reconnecting += _ =>
            {
                SetState(ConnectionState.Reconnecting);
                return Task.CompletedTask;
            };
            _connection.Reconnected += _ =>
            {
                SetState(ConnectionState.Connected);
                return Task.CompletedTask;
            };
            _connection.Closed += _ =>
            {
                SetState(ConnectionState.Disconnected);
                return Task.CompletedTask;
            };
        }

        public async Task ConnectAsync(CancellationToken ct = default)
        {
            SetState(ConnectionState.Connecting);
            try
            {
                await _connection.StartAsync(ct);
                SetState(ConnectionState.Connected);
            }
            catch
            {
                SetState(ConnectionState.Disconnected);
                throw;
            }
        }

        public bool IsConnected => _connection.State == HubConnectionState.Connected;

        public async Task<IReadOnlyList<RoomSummaryDto>> ListRoomsAsync(CancellationToken ct = default)
        {
            if (_connection.State != HubConnectionState.Connected)
                return Array.Empty<RoomSummaryDto>();

            return await _connection.InvokeAsync<IReadOnlyList<RoomSummaryDto>>("ListRooms", ct);
        }

        // =========================================================
        // HUB METHODS (Phase 3)
        // Trả null nếu chưa Connected — caller tự xử lý như mất kết nối.
        // =========================================================

        public async Task<HubResult<CreateRoomResultDto>?> CreateRoomAsync(
            int setId, CancellationToken ct = default)
        {
            if (_connection.State != HubConnectionState.Connected) return null;
            return await _connection.InvokeAsync<HubResult<CreateRoomResultDto>>(
                "CreateRoom", new CreateRoomRequest { SetId = setId }, ct);
        }

        public async Task<HubResult<JoinRoomResultDto>?> JoinRoomAsync(
            string roomCode, CancellationToken ct = default)
        {
            if (_connection.State != HubConnectionState.Connected) return null;
            return await _connection.InvokeAsync<HubResult<JoinRoomResultDto>>(
                "JoinRoom", roomCode, ct);
        }

        public async Task<HubResult<RoomStateDto>?> LeaveRoomAsync(
            CancellationToken ct = default)
        {
            if (_connection.State != HubConnectionState.Connected) return null;
            return await _connection.InvokeAsync<HubResult<RoomStateDto>>("LeaveRoom", ct);
        }

        public async Task<HubResult<RoomStateDto>?> ChangeSetAsync(
            int setId, CancellationToken ct = default)
        {
            if (_connection.State != HubConnectionState.Connected) return null;
            return await _connection.InvokeAsync<HubResult<RoomStateDto>>(
                "ChangeSet", setId, ct);
        }

        public async Task<HubResult<RoomStateDto>?> StartGameAsync(
            CancellationToken ct = default)
        {
            if (_connection.State != HubConnectionState.Connected) return null;
            return await _connection.InvokeAsync<HubResult<RoomStateDto>>("StartGame", ct);
        }

        public async Task<HubResult<RoomStateDto>?> GetRoomStateAsync(
            CancellationToken ct = default)
        {
            if (_connection.State != HubConnectionState.Connected) return null;
            return await _connection.InvokeAsync<HubResult<RoomStateDto>>("GetRoomState", ct);
        }

        // Kick do host thực hiện. Server-side Hub method "Kick" cần thêm (xem mục 3).
        public async Task<HubResult<bool>?> KickAsync(
            string roomCode, int targetUserId, string reason, CancellationToken ct = default)
        {
            if (_connection.State != HubConnectionState.Connected) return null;
            return await _connection.InvokeAsync<HubResult<bool>>(
                "Kick", roomCode, targetUserId, reason, ct);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await _connection.DisposeAsync();
            }
            catch
            {
                // Bỏ qua — dispose không bao giờ được throw lên UI.
            }
        }

        private void SetState(ConnectionState s)
        {
            State = s;
            StateChanged?.Invoke(s);
        }

        private static string ReadReason(JsonElement el)
        {
            try
            {
                if (el.ValueKind == JsonValueKind.Object
                    && el.TryGetProperty("Reason", out var r))
                    return r.GetString() ?? "";
                return el.ToString();
            }
            catch { return ""; }
        }
    }
}