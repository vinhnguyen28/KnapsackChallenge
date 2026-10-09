using System;
using System.Collections.Generic;
using System.Linq;

namespace KnapsackChallenge.Server.Services.Rooms
{
    /// <summary>
    /// Theo dõi ánh xạ giữa userId, connectionId và roomCode hiện tại.
    /// - Một user có thể có NHIỀU connection (nhiều cửa sổ / thiết bị).
    /// - Khi bị kick / ban / room closed, ta cần biết connectionId nào đang
    ///   nằm trong group "room:{code}" để gỡ khỏi group qua IHubContext.
    /// Thread-safe bằng lock đơn giản (thao tác chỉ là đọc/ghi dictionary).
    /// </summary>
    public sealed class ConnectionTracker
    {
        private readonly object _lock = new();

        // userId  ->  tập connectionId
        private readonly Dictionary<int, HashSet<string>> _userToConnections = new();

        // connectionId  ->  userId
        private readonly Dictionary<string, int> _connectionToUser = new();

        // connectionId  ->  roomCode (chỉ tồn tại khi connection đang nằm trong 1 phòng)
        private readonly Dictionary<string, string> _connectionToRoom = new();

        /// <summary>Đăng ký 1 connection cho user. roomCode có thể null (chưa vào phòng).</summary>
        public void Track(int userId, string connectionId, string? roomCode)
        {
            if (string.IsNullOrEmpty(connectionId)) return;

            lock (_lock)
            {
                if (!_userToConnections.TryGetValue(userId, out var set))
                {
                    set = new HashSet<string>();
                    _userToConnections[userId] = set;
                }
                set.Add(connectionId);
                _connectionToUser[connectionId] = userId;

                if (!string.IsNullOrEmpty(roomCode))
                    _connectionToRoom[connectionId] = roomCode;
                else
                    _connectionToRoom.Remove(connectionId);
            }
        }

        /// <summary>Gán / cập nhật roomCode cho connection đã track.</summary>
        public void SetRoom(string connectionId, string roomCode)
        {
            if (string.IsNullOrEmpty(connectionId) || string.IsNullOrEmpty(roomCode)) return;

            lock (_lock)
            {
                if (_connectionToUser.ContainsKey(connectionId))
                    _connectionToRoom[connectionId] = roomCode;
            }
        }

        /// <summary>Xoá liên kết connection &lt;-&gt; room, vẫn giữ liên kết user &lt;-&gt; connection.</summary>
        public void ClearRoom(string connectionId)
        {
            if (string.IsNullOrEmpty(connectionId)) return;
            lock (_lock) { _connectionToRoom.Remove(connectionId); }
        }

        /// <summary>Gỡ hoàn toàn 1 connection (disconnect hoặc rời hẳn).</summary>
        public void Untrack(string connectionId)
        {
            if (string.IsNullOrEmpty(connectionId)) return;

            lock (_lock)
            {
                _connectionToRoom.Remove(connectionId);

                if (_connectionToUser.TryGetValue(connectionId, out var userId))
                {
                    _connectionToUser.Remove(connectionId);

                    if (_userToConnections.TryGetValue(userId, out var set))
                    {
                        set.Remove(connectionId);
                        if (set.Count == 0)
                            _userToConnections.Remove(userId);
                    }
                }
            }
        }

        /// <summary>roomCode hiện tại của connection (null nếu chưa vào phòng).</summary>
        public string? GetRoom(string connectionId)
        {
            if (string.IsNullOrEmpty(connectionId)) return null;

            lock (_lock)
            {
                return _connectionToRoom.TryGetValue(connectionId, out var room) ? room : null;
            }
        }

        /// <summary>Tất cả connectionId của 1 user (dùng khi kick/ban user).</summary>
        public IReadOnlyList<string> GetConnections(int userId)
        {
            lock (_lock)
            {
                return _userToConnections.TryGetValue(userId, out var set)
                    ? set.ToArray()
                    : Array.Empty<string>();
            }
        }

        /// <summary>Tất cả connectionId đang nằm trong 1 phòng (dùng khi room closed).</summary>
        public IReadOnlyList<string> GetConnectionsInRoom(string roomCode)
        {
            if (string.IsNullOrEmpty(roomCode)) return Array.Empty<string>();

            lock (_lock)
            {
                return _connectionToRoom
                    .Where(kvp => string.Equals(kvp.Value, roomCode, StringComparison.OrdinalIgnoreCase))
                    .Select(kvp => kvp.Key)
                    .ToArray();
            }
        }
    }
}