using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Data.Entities;
using Microsoft.Data.SqlClient;
using System.Data;

namespace KnapsackChallenge.Data.Repositories
{
    public class UserRepository
    {
        private readonly DbConnectionHelper _dbHelper;

        public UserRepository()
        {
            _dbHelper = new DbConnectionHelper();
        }

        // v7: bổ sung Hearts, LastHeartRefillAt ở CUỐI để không đổi index cũ.
        private const string AuthColumns =
            "Id, Username, PasswordHash, Role, IsBanned, BanReason, BannedAt, BannedBy, " +
            "CreatedAt, LastLoginAt, LastSeenAt, Hearts, LastHeartRefillAt, TotalExp, Level";

        private static UserEntity MapUser(IDataRecord r) => new UserEntity
        {
            Id = r.GetInt32(0),
            Username = r.IsDBNull(1) ? null : r.GetString(1),
            PasswordHash = r.IsDBNull(2) ? null : r.GetString(2),
            Role = r.IsDBNull(3) ? null : r.GetString(3),
            IsBanned = !r.IsDBNull(4) && r.GetBoolean(4),
            BanReason = r.IsDBNull(5) ? null : r.GetString(5),
            BannedAt = r.IsDBNull(6) ? null : r.GetDateTime(6),
            BannedBy = r.IsDBNull(7) ? null : r.GetString(7),
            CreatedAt = r.IsDBNull(8) ? default : r.GetDateTime(8),
            LastLoginAt = r.IsDBNull(9) ? null : r.GetDateTime(9),
            LastSeenAt = r.IsDBNull(10) ? null : r.GetDateTime(10),
            Hearts = r.IsDBNull(11) ? 25 : r.GetInt32(11),
            LastHeartRefillAt = r.IsDBNull(12) ? null : r.GetDateTime(12),
            TotalExp = r.IsDBNull(13) ? 0L : r.GetInt64(13),
            Level = r.IsDBNull(14) ? 1 : r.GetInt32(14),
        };

        // ---------- AUTH ----------

        public UserEntity? GetUserByUsername(string username)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {AuthColumns} FROM Users WHERE Username = @Username";
            AddParameter(command, "@Username", username);
            using var reader = command.ExecuteReader();
            return reader.Read() ? MapUser(reader) : null;
        }

        public UserEntity? GetById(int id)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {AuthColumns} FROM Users WHERE Id = @Id";
            AddParameter(command, "@Id", id);
            using var reader = command.ExecuteReader();
            return reader.Read() ? MapUser(reader) : null;
        }

        public bool CreateUser(string username, string passwordHash, string role)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO Users (Username, PasswordHash, Role) VALUES (@Username, @PasswordHash, @Role)";
            AddParameter(command, "@Username", username);
            AddParameter(command, "@PasswordHash", passwordHash);
            AddParameter(command, "@Role", role);
            try
            {
                command.ExecuteNonQuery();
                return true;
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                return false;
            }
        }

        // ---------- HEARTBEAT ----------

        public void MarkLogin(int userId)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "UPDATE Users SET LastLoginAt = SYSUTCDATETIME(), LastSeenAt = SYSUTCDATETIME() WHERE Id = @Id";
            AddParameter(command, "@Id", userId);
            command.ExecuteNonQuery();
        }

        public void UpdateLastSeen(int userId, bool online)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = online
                ? "UPDATE Users SET LastSeenAt = SYSUTCDATETIME() WHERE Id = @Id"
                : "UPDATE Users SET LastSeenAt = NULL WHERE Id = @Id";
            AddParameter(command, "@Id", userId);
            command.ExecuteNonQuery();
        }

        // ---------- v7: HEARTS ----------

        // Đọc tim + mốc hồi gần nhất. Trả (0, null) nếu user không tồn tại.
        public (int Hearts, DateTime? LastRefillAt) GetHearts(int userId)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT Hearts, LastHeartRefillAt FROM Users WHERE Id = @Id";
            AddParameter(command, "@Id", userId);
            using var reader = command.ExecuteReader();
            if (!reader.Read()) return (0, null);

            int hearts = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
            DateTime? last = reader.IsDBNull(1) ? null : reader.GetDateTime(1);
            return (hearts, last);
        }

        // Ghi tim + mốc hồi. lastRefillAt = null -> set NULL.
        public void UpdateHearts(int userId, int hearts, DateTime? lastRefillAt)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Users
                SET Hearts = @Hearts,
                    LastHeartRefillAt = @LastRefillAt
                WHERE Id = @Id";
            AddParameter(command, "@Id", userId);
            AddParameter(command, "@Hearts", hearts);
            AddParameter(command, "@LastRefillAt", (object?)lastRefillAt ?? DBNull.Value);
            command.ExecuteNonQuery();
        }

        // ---------- v7+: TIM NGUYÊN TỬ ----------

        // CAS: chỉ ghi nếu (Hearts, LastHeartRefillAt) vẫn đúng như lúc đọc.
        // Trả true nếu UPDATE thành công (1 dòng), false nếu có luồng khác đã sửa.
        // Lưu ý: 2 tham số datetime phải là SqlDbType.DateTime2 để so sánh khớp
        // với cột DATETIME2 (kiểu DateTime mặc định có độ chính xác thấp hơn, dễ sai).
        public bool TrySetHeartsIfUnchanged(int userId,
                                            int expectedHearts,
                                            DateTime? expectedLast,
                                            int newHearts,
                                            DateTime? newLast)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Users
                SET Hearts = @NewHearts,
                    LastHeartRefillAt = @NewLast
                WHERE Id = @Id
                  AND Hearts = @ExpectedHearts
                  AND ((LastHeartRefillAt IS NULL AND @ExpectedLast IS NULL)
                       OR LastHeartRefillAt = @ExpectedLast)";
            AddParameter(command, "@Id", userId);
            AddParameter(command, "@NewHearts", newHearts);
            AddParameter(command, "@ExpectedHearts", expectedHearts);
            AddDateTime2Parameter(command, "@NewLast", newLast);
            AddDateTime2Parameter(command, "@ExpectedLast", expectedLast);
            return command.ExecuteNonQuery() == 1;
        }

        // Trừ tim nguyên tử: chỉ thành công nếu Hearts > 0.
        // - Nếu đang max hoặc chưa có mốc hồi → LastHeartRefillAt = @Now (bắt đầu đếm).
        // - Ngược lại → giữ nguyên LastHeartRefillAt cũ.
        // (Trong 1 câu UPDATE của SQL Server, mọi tham chiếu cột ở SET/WHERE đều dùng
        //  giá trị TRƯỚC khi update, nên CASE bên dưới là ngữ nghĩa "trước khi trừ".)
        public (bool Success, int Hearts, DateTime? LastRefillAt) TryConsumeHeart(
            int userId, int maxHearts, DateTime nowUtc)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Users
                SET Hearts = Hearts - 1,
                    LastHeartRefillAt = CASE
                        WHEN Hearts >= @Max OR LastHeartRefillAt IS NULL THEN @Now
                        ELSE LastHeartRefillAt
                    END
                OUTPUT inserted.Hearts, inserted.LastHeartRefillAt
                WHERE Id = @Id AND Hearts > 0";
            AddParameter(command, "@Id", userId);
            AddParameter(command, "@Max", maxHearts);
            AddDateTime2Parameter(command, "@Now", nowUtc);

            using var reader = command.ExecuteReader();
            if (!reader.Read()) return (false, 0, null);

            int hearts = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
            DateTime? last = reader.IsDBNull(1) ? null : reader.GetDateTime(1);
            return (true, hearts, last);
        }

        // Helper tạo SqlParameter tường minh cho cột DATETIME2.
        // Không dùng AddParameter cũ vì CreateParameter() sẽ suy luận SqlDbType.DateTime.
        private static void AddDateTime2Parameter(IDbCommand command, string name, DateTime? value)
        {
            var p = new SqlParameter(name, SqlDbType.DateTime2)
            {
                Value = (object?)value ?? DBNull.Value
            };
            command.Parameters.Add(p);
        }

        // ---------- v8: RANK & EXP ----------

        // Đọc TotalExp + Level hiện tại. Trả (0, 1) nếu user không tồn tại.
        public (long TotalExp, int Level) GetExpAndLevel(int userId)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT TotalExp, Level FROM Users WHERE Id = @Id";
            AddParameter(command, "@Id", userId);
            using var reader = command.ExecuteReader();
            if (!reader.Read()) return (0L, 1);

            long exp = reader.IsDBNull(0) ? 0L : reader.GetInt64(0);
            int level = reader.IsDBNull(1) ? 1 : reader.GetInt32(1);
            return (exp, level);
        }

        // Cập nhật TotalExp + Level (Level đã tính sẵn ở Core).
        // Cập nhật TotalExp + Level (Level đã tính sẵn ở Core).
        public void UpdateExpAndLevel(int userId, long newTotalExp, int newLevel)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
        UPDATE Users
        SET TotalExp = @Exp,
            Level    = @Level
        WHERE Id = @Id";
            AddParameter(command, "@Id", userId);
            AddParameter(command, "@Exp", newTotalExp);
            AddParameter(command, "@Level", newLevel);
            command.ExecuteNonQuery();
        }

        // ---------- v8+: EXP NGUYÊN TỬ ----------

        // Cộng EXP nguyên tử bằng OUTPUT. Trả (EXP trước, EXP sau) hoặc null nếu user không tồn tại.
        // Không dùng read-then-write để tránh mất EXP khi 2 ván cùng kết thúc.
        public (long OldExp, long NewExp)? AddExpAtomic(int userId, int expToAdd)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Users
                SET TotalExp = TotalExp + @Gain
                OUTPUT deleted.TotalExp, inserted.TotalExp
                WHERE Id = @Id";
            AddParameter(command, "@Id", userId);
            AddParameter(command, "@Gain", expToAdd);

            using var reader = command.ExecuteReader();
            if (!reader.Read()) return null;

            long oldExp = reader.IsDBNull(0) ? 0L : reader.GetInt64(0);
            long newExp = reader.IsDBNull(1) ? 0L : reader.GetInt64(1);
            return (oldExp, newExp);
        }

        // Chỉ NÂNG Level, không hạ cấp. Dùng cho luồng cộng EXP để một luồng chậm
        // không ghi đè Level thấp hơn lên Level cao hơn đã được luồng khác set.
        public void RaiseLevelTo(int userId, int level)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Users SET Level = @Level WHERE Id = @Id AND Level < @Level";
            AddParameter(command, "@Id", userId);
            AddParameter(command, "@Level", level);
            command.ExecuteNonQuery();
        }

        // Chỉ cập nhật Level, TUYỆT ĐỐI không đụng TotalExp.
        // Dùng cho RankService.GetStatus khi phát hiện DB lệch Level so với EXP.
        public void SetLevel(int userId, int level)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Users SET Level = @Level WHERE Id = @Id";
            AddParameter(command, "@Id", userId);
            AddParameter(command, "@Level", level);
            command.ExecuteNonQuery();
        }

        // ---------- v9: BẢNG XẾP HẠNG EXP ----------

        // Top người chơi theo TotalExp giảm dần. Loại Admin + tài khoản bị ban + chưa có EXP.
        // KHÔNG điền RankTitle — Core sẽ tính (RankRules nằm ở Core).
        public List<ExpLeaderboardEntryDto> GetExpLeaderboard(int topN)
        {
            if (topN < 1) topN = 100;

            var list = new List<ExpLeaderboardEntryDto>();
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT TOP (@TopN)
                       ROW_NUMBER() OVER (ORDER BY TotalExp DESC, Id ASC) AS Pos,
                       Id, Username, TotalExp, Level
                FROM Users
                WHERE IsBanned = 0 AND Role <> N'Admin' AND TotalExp > 0
                ORDER BY TotalExp DESC, Id ASC";
            AddParameter(command, "@TopN", topN);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ExpLeaderboardEntryDto
                {
                    Rank = reader.IsDBNull(0) ? 0 : (int)reader.GetInt64(0),
                    UserId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    Username = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    TotalExp = reader.IsDBNull(3) ? 0L : reader.GetInt64(3),
                    Level = reader.IsDBNull(4) ? 1 : reader.GetInt32(4),
                });
            }
            return list;
        }

        // Hạng EXP của 1 người. Trả null nếu user không tồn tại / bị ban / chưa có EXP.
        // Pos = COUNT(*) những người xếp trên + 1 (tie-break theo Id ASC cho ổn định).
        public ExpLeaderboardEntryDto? GetUserExpRank(int userId)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT u.Id, u.Username, u.TotalExp, u.Level,
                       (SELECT COUNT(*) FROM Users o
                        WHERE o.IsBanned = 0 AND o.Role <> N'Admin' AND o.TotalExp > 0
                          AND (o.TotalExp > u.TotalExp
                               OR (o.TotalExp = u.TotalExp AND o.Id < u.Id))) + 1 AS Pos
                FROM Users u
                WHERE u.Id = @UserId AND u.IsBanned = 0 AND u.TotalExp > 0";
            AddParameter(command, "@UserId", userId);

            using var reader = command.ExecuteReader();
            if (!reader.Read()) return null;

            return new ExpLeaderboardEntryDto
            {
                Rank = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                UserId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                Username = reader.IsDBNull(1) ? "" : reader.GetString(1),
                TotalExp = reader.IsDBNull(2) ? 0L : reader.GetInt64(2),
                Level = reader.IsDBNull(3) ? 1 : reader.GetInt32(3),
            };
        }



        // ---------- ADMIN ----------

        public void SetBanState(int userId, bool isBanned, string? reason, string adminUsername)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Users SET
                    IsBanned  = @IsBanned,
                    BanReason = CASE WHEN @IsBanned = 1 THEN @Reason        ELSE NULL END,
                    BannedAt  = CASE WHEN @IsBanned = 1 THEN SYSUTCDATETIME() ELSE NULL END,
                    BannedBy  = CASE WHEN @IsBanned = 1 THEN @AdminUsername ELSE NULL END
                WHERE Id = @Id";
            AddParameter(command, "@Id", userId);
            AddParameter(command, "@IsBanned", isBanned);
            AddParameter(command, "@Reason", (object?)reason ?? DBNull.Value);
            AddParameter(command, "@AdminUsername", adminUsername);
            command.ExecuteNonQuery();
        }

        public List<UserListItemDto> GetAllForAdmin()
        {
            var list = new List<UserListItemDto>();
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Username, Role, IsBanned, BanReason, BannedAt, BannedBy,
                       CreatedAt, LastLoginAt, LastSeenAt
                FROM Users
                ORDER BY Id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new UserListItemDto
                {
                    Id = reader.GetInt32(0),
                    Username = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Role = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    IsBanned = !reader.IsDBNull(3) && reader.GetBoolean(3),
                    BanReason = reader.IsDBNull(4) ? null : reader.GetString(4),
                    BannedAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    BannedBy = reader.IsDBNull(6) ? null : reader.GetString(6),
                    CreatedAt = reader.IsDBNull(7) ? default : reader.GetDateTime(7),
                    LastLoginAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                    LastSeenAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                });
            }
            return list;
        }

        public int CountTodayRegistrations()
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(*) FROM Users WHERE CAST(CreatedAt AS DATE) = CAST(SYSUTCDATETIME() AS DATE)";
            return Convert.ToInt32(command.ExecuteScalar());
        }

        private static void AddParameter(IDbCommand command, string name, object value)
        {
            var param = command.CreateParameter();
            param.ParameterName = name;
            param.Value = value;
            command.Parameters.Add(param);
        }
    }
}