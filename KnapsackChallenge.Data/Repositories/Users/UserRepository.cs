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

        // Cộng EXP và cập nhật Level. Trả về (NewTotalExp, NewLevel).
        // Level được tính lại ở Core, KHÔNG tin số liệu từ caller.
        //public (long NewTotalExp, int NewLevel) AddExp(int userId, int expToAdd)
        //{
        //    using var connection = _dbHelper.CreateConnection();
        //    connection.Open();


        //    long currentExp;
        //    int currentLevel;
        //    using (var read = connection.CreateCommand())
        //    {
        //        read.CommandText = "SELECT TotalExp, Level FROM Users WHERE Id = @Id";
        //        AddParameter(read, "@Id", userId);
        //        using var reader = read.ExecuteReader();
        //        if (!reader.Read()) return (0L, 1);
        //        currentExp = reader.IsDBNull(0) ? 0L : reader.GetInt64(0);
        //        currentLevel = reader.IsDBNull(1) ? 1 : reader.GetInt32(1);
        //    }

        //    long newExp = currentExp + Math.Max(0, expToAdd);
        //    int newLevel = Algorithms.RankRules.GetLevelFromExp(newExp);


        //    if (newExp != currentExp || newLevel != currentLevel)
        //    {
        //        using var update = connection.CreateCommand();
        //        update.CommandText = @"
        //    UPDATE Users
        //    SET TotalExp = @Exp,
        //        Level    = @Level
        //    WHERE Id = @Id";
        //        AddParameter(update, "@Id", userId);
        //        AddParameter(update, "@Exp", newExp);
        //        AddParameter(update, "@Level", newLevel);
        //        update.ExecuteNonQuery();
        //    }

        //    return (newExp, newLevel);
        //}

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