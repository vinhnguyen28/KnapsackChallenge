namespace KnapsackChallenge.UI.Features.Player
{
    // Lưu JWT + userId + username trong RAM cho phiên Player hiện tại.
    // KHÔNG persist — mất khi đóng app là điều mong muốn.
    // Thread-safe (dùng lock; singleton readonly).
    public sealed class MultiplayerSession
    {
        // Dùng factory delegate để Lazy<T> gọi constructor private.
        private static readonly Lazy<MultiplayerSession> _instance = new(() => new MultiplayerSession());

        public static MultiplayerSession Instance => _instance.Value;

        private readonly object _lock = new();
        private string? _token;
        private int _userId;
        private string? _username;

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
            lock (_lock)
            {
                _token = token;
                _userId = userId;
                _username = username;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _token = null;
                _userId = 0;
                _username = null;
            }
        }
    }
}