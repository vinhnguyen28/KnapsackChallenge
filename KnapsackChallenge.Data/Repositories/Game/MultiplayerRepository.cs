using System.Data;
using Microsoft.Data.SqlClient;
using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Common.Enums;
using KnapsackChallenge.Data.Entities;

namespace KnapsackChallenge.Data.Repositories
{
    // Repository cho phòng chơi multiplayer.
    // - ADO.NET thuần, mọi tham số là SqlParameter.
    // - Dùng DbConnectionHelper giống các repository hiện có.
    // - Tất cả timestamp là UTC (DateTime.UtcNow từ Server truyền xuống).
    public class MultiplayerRepository
    {
        private readonly DbConnectionHelper _dbHelper = new();

        // =========================================================
        // 1. Tạo phòng — INSERT GameSessions + host vào RoomPlayers
        //    trong MỘT TRANSACTION. Trả về SessionId vừa tạo.
        // =========================================================
        public int CreateRoom(string roomCode, int hostUserId, int setId, DateTime createdAtUtc)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();

            try
            {
                int sessionId;
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        INSERT INTO GameSessions
                            (RoomCode, Status, SetId, Mode, HostUserId, CreatedAt)
                        OUTPUT INSERTED.Id
                        VALUES (@RoomCode, 'Waiting', @SetId, 'Multiplayer', @HostUserId, @CreatedAt)";
                    AddParameter(cmd, "@RoomCode", roomCode);
                    AddParameter(cmd, "@SetId", setId);
                    AddParameter(cmd, "@HostUserId", hostUserId);
                    AddParameter(cmd, "@CreatedAt", createdAtUtc);
                    sessionId = (int)cmd.ExecuteScalar()!;
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        INSERT INTO RoomPlayers
                            (SessionId, UserId, TotalScore, TotalWeight, IsSubmitted,
                             IsHost, JoinedAt, IsKicked, IsBanned)
                        VALUES (@SessionId, @UserId, 0, 0, 0, 1, @JoinedAt, 0, 0)";
                    AddParameter(cmd, "@SessionId", sessionId);
                    AddParameter(cmd, "@UserId", hostUserId);
                    AddParameter(cmd, "@JoinedAt", createdAtUtc);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
                return sessionId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // =========================================================
        // 2. Thêm người vào phòng (không phải host)
        // =========================================================
        public void AddPlayer(int sessionId, int userId, DateTime joinedAtUtc)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO RoomPlayers
                    (SessionId, UserId, TotalScore, TotalWeight, IsSubmitted,
                     IsHost, JoinedAt, IsKicked, IsBanned)
                VALUES (@SessionId, @UserId, 0, 0, 0, 0, @JoinedAt, 0, 0)";
            AddParameter(cmd, "@SessionId", sessionId);
            AddParameter(cmd, "@UserId", userId);
            AddParameter(cmd, "@JoinedAt", joinedAtUtc);
            cmd.ExecuteNonQuery();
        }

        // =========================================================
        // 3. Xoá 1 người khỏi phòng (Leave)
        // =========================================================
        public void RemovePlayer(int sessionId, int userId)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "DELETE FROM RoomPlayers WHERE SessionId = @SessionId AND UserId = @UserId";
            AddParameter(cmd, "@SessionId", sessionId);
            AddParameter(cmd, "@UserId", userId);
            cmd.ExecuteNonQuery();
        }

        // =========================================================
        // 4. Chuyển host — cập nhật cả RoomPlayers lẫn GameSessions
        //    để admin list luôn thấy host đúng.
        // =========================================================
        public void TransferHost(int sessionId, int newHostUserId)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();

            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "UPDATE RoomPlayers SET IsHost = 0 WHERE SessionId = @SessionId";
                    AddParameter(cmd, "@SessionId", sessionId);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText =
                        "UPDATE RoomPlayers SET IsHost = 1 WHERE SessionId = @SessionId AND UserId = @UserId";
                    AddParameter(cmd, "@SessionId", sessionId);
                    AddParameter(cmd, "@UserId", newHostUserId);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText =
                        "UPDATE GameSessions SET HostUserId = @UserId WHERE Id = @SessionId";
                    AddParameter(cmd, "@SessionId", sessionId);
                    AddParameter(cmd, "@UserId", newHostUserId);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // =========================================================
        // 5. Đổi bộ đề khi phòng còn Waiting
        // =========================================================
        public void UpdateSet(int sessionId, int setId)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE GameSessions SET SetId = @SetId WHERE Id = @SessionId";
            AddParameter(cmd, "@SessionId", sessionId);
            AddParameter(cmd, "@SetId", setId);
            cmd.ExecuteNonQuery();
        }

        // =========================================================
        // 6. Đánh dấu ván bắt đầu — chỉ áp dụng khi đang Waiting
        // =========================================================
        public bool MarkPlaying(int sessionId, int optimalValue, DateTime startedAtUtc)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE GameSessions
                SET Status = 'Playing',
                    StartedAt = @StartedAt,
                    OptimalValue = @OptimalValue
                WHERE Id = @SessionId AND Status = 'Waiting'";
            AddParameter(cmd, "@SessionId", sessionId);
            AddParameter(cmd, "@StartedAt", startedAtUtc);
            AddParameter(cmd, "@OptimalValue", optimalValue);
            return cmd.ExecuteNonQuery() > 0;
        }

        // =========================================================
        // 7. Lưu kết quả 1 người nộp bài — UPDATE RoomPlayers +
        //    replace SelectedItems trong MỘT TRANSACTION.
        // =========================================================
        public void SaveSubmission(int sessionId, int userId,
                                    IReadOnlyList<int> selectedItemIds,
                                    int totalScore, int totalWeight, int timeSpentSeconds)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();

            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        UPDATE RoomPlayers
                        SET TotalScore = @Score,
                            TotalWeight = @Weight,
                            IsSubmitted = 1,
                            TimeSpentSeconds = @TimeSpent
                        WHERE SessionId = @SessionId AND UserId = @UserId";
                    AddParameter(cmd, "@SessionId", sessionId);
                    AddParameter(cmd, "@UserId", userId);
                    AddParameter(cmd, "@Score", totalScore);
                    AddParameter(cmd, "@Weight", totalWeight);
                    AddParameter(cmd, "@TimeSpent", timeSpentSeconds);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText =
                        "DELETE FROM SelectedItems WHERE SessionId = @SessionId AND UserId = @UserId";
                    AddParameter(cmd, "@SessionId", sessionId);
                    AddParameter(cmd, "@UserId", userId);
                    cmd.ExecuteNonQuery();
                }

                foreach (var itemId in selectedItemIds.Distinct())
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        INSERT INTO SelectedItems (SessionId, UserId, ItemId)
                        VALUES (@SessionId, @UserId, @ItemId)";
                    AddParameter(cmd, "@SessionId", sessionId);
                    AddParameter(cmd, "@UserId", userId);
                    AddParameter(cmd, "@ItemId", itemId);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // =========================================================
        // 8. Đánh dấu ván kết thúc
        // =========================================================
        public void MarkFinished(int sessionId, DateTime finishedAtUtc)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE GameSessions
                SET Status = 'Finished',
                    FinishedAt = @FinishedAt
                WHERE Id = @SessionId";
            AddParameter(cmd, "@SessionId", sessionId);
            AddParameter(cmd, "@FinishedAt", finishedAtUtc);
            cmd.ExecuteNonQuery();
        }

        // =========================================================
        // 9. Xoá phòng (chỉ khi chưa từng Playing)
        // =========================================================
        public void DeleteRoom(int sessionId)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();

            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "DELETE FROM SelectedItems WHERE SessionId = @SessionId";
                    AddParameter(cmd, "@SessionId", sessionId);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "DELETE FROM RoomPlayers WHERE SessionId = @SessionId";
                    AddParameter(cmd, "@SessionId", sessionId);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "DELETE FROM GameSessions WHERE Id = @SessionId";
                    AddParameter(cmd, "@SessionId", sessionId);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // =========================================================
        // 10a. Đánh dấu người bị kick
        // =========================================================
        public void MarkKicked(int sessionId, int userId)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE RoomPlayers
                SET IsKicked = 1
                WHERE SessionId = @SessionId AND UserId = @UserId";
            AddParameter(cmd, "@SessionId", sessionId);
            AddParameter(cmd, "@UserId", userId);
            cmd.ExecuteNonQuery();
        }

        // =========================================================
        // 10b. Đánh dấu người bị ban — cột riêng để Admin phân biệt
        // =========================================================
        public void MarkBanned(int sessionId, int userId)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE RoomPlayers
                SET IsBanned = 1
                WHERE SessionId = @SessionId AND UserId = @UserId";
            AddParameter(cmd, "@SessionId", sessionId);
            AddParameter(cmd, "@UserId", userId);
            cmd.ExecuteNonQuery();
        }

        // =========================================================
        // 11. Danh sách phòng đang hoạt động (Waiting/Playing) cho Admin
        // =========================================================
        public List<RoomSummaryDto> ListActiveRooms()
        {
            var list = new List<RoomSummaryDto>();
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT gs.Id,
                       gs.RoomCode,
                       gs.Status,
                       u.Username,
                       ks.SetName,
                       (SELECT COUNT(*) FROM RoomPlayers rp WHERE rp.SessionId = gs.Id) AS PlayerCount,
                       ISNULL((SELECT MaxPlayers FROM GameModes WHERE ModeKey = 'Multiplayer'), 4) AS MaxPlayers,
                       gs.CreatedAt,
                       gs.StartedAt
                FROM GameSessions gs
                INNER JOIN Users u ON u.Id = gs.HostUserId
                LEFT  JOIN KnapsackSets ks ON ks.Id = gs.SetId
                WHERE gs.Mode = 'Multiplayer'
                  AND gs.Status IN ('Waiting', 'Playing')
                ORDER BY gs.CreatedAt DESC";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new RoomSummaryDto
                {
                    SessionId = reader.GetInt32(0),
                    RoomCode = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Status = ParseRoomStatus(reader.IsDBNull(2) ? "" : reader.GetString(2)),
                    HostUsername = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    SetName = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    PlayerCount = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    MaxPlayers = reader.IsDBNull(6) ? 4 : reader.GetInt32(6),
                    CreatedAtUtc = reader.IsDBNull(7) ? default : reader.GetDateTime(7),
                    StartedAtUtc = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                });
            }
            return list;
        }

        // =========================================================
        // 12. Kiểm tra nhanh IsBanned
        // =========================================================
        public (bool Exists, bool IsBanned, string? Reason) CheckUserBanned(int userId)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT IsBanned, BanReason FROM Users WHERE Id = @Id";
            AddParameter(cmd, "@Id", userId);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return (false, false, null);

            bool banned = !reader.IsDBNull(0) && reader.GetBoolean(0);
            string? reason = reader.IsDBNull(1) ? null : reader.GetString(1);
            return (true, banned, reason);
        }

        // =========================================================
        // Helpers
        // =========================================================

        private static RoomStatus ParseRoomStatus(string s) => s switch
        {
            "Waiting" => RoomStatus.Waiting,
            "Playing" => RoomStatus.Playing,
            "Finished" => RoomStatus.Finished,
            _ => RoomStatus.Closed,
        };

        private static void AddParameter(IDbCommand command, string name, object value)
        {
            var param = command.CreateParameter();
            param.ParameterName = name;
            param.Value = value;
            command.Parameters.Add(param);
        }
    }
}