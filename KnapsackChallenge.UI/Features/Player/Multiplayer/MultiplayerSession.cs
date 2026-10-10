namespace KnapsackChallenge.UI.Features.Player
{
    public sealed class MultiplayerSession
    {
        private static readonly Lazy<MultiplayerSession> _instance = new(() => new MultiplayerSession());
        public static MultiplayerSession Instance => _instance.Value;

        private readonly object _lock = new();
        private string? _token;
        private int _userId;
        private string? _username;
        private MultiplayerConnectionService? _connection;

        private MultiplayerSession() { }

        public bool HasToken
        {
            get { lock (_lock) return !string.IsNullOrEmpty(_token); }
        }

        public (string? Token, int UserId, string? Username) GetSnapshot()
        {
            lock (_lock) return (_token, _userId, _username);
        }

        public void Set(string token, int userId, string username)
        {
            lock (_lock) { _token = token; _userId = userId; _username = username; }
        }

        public void Clear()
        {
            lock (_lock) { _token = null; _userId = 0; _username = null; }
        }

        // ===== Connection lifecycle (Phase 3) =====

        public MultiplayerConnectionService? Connection
        {
            get { lock (_lock) return _connection; }
        }

        // Get-or-create: trả về connection đang Connected, hoặc tạo mới.
        public async Task<MultiplayerConnectionService> EnsureConnectionAsync(CancellationToken ct = default)
        {
            MultiplayerConnectionService? existing;
            lock (_lock) existing = _connection;

            if (existing != null && existing.IsConnected) return existing;
            if (existing != null)
            {
                try { await existing.DisposeAsync(); } catch { /* ignore */ }
            }

            string? token;
            lock (_lock) token = _token;
            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("Chưa có JWT. Hãy đăng nhập lại.");

            var conn = new MultiplayerConnectionService(MultiplayerServerConfig.ServerUrl, token);
            await conn.ConnectAsync(ct);

            lock (_lock) _connection = conn;
            return conn;
        }

        public async Task DisposeConnectionAsync()
        {
            MultiplayerConnectionService? conn;
            lock (_lock) { conn = _connection; _connection = null; }
            if (conn != null)
            {
                try { await conn.DisposeAsync(); } catch { /* ignore */ }
            }
        }
    }
}