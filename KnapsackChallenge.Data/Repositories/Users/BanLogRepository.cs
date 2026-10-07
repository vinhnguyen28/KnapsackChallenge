using System.Data;
using KnapsackChallenge.Data.Entities;

namespace KnapsackChallenge.Data.Repositories
{
    public class BanLogRepository
    {
        private readonly DbConnectionHelper _dbHelper = new();

        public void Add(int userId, string action, string? reason, string adminUsername)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO BanLogs (UserId, Action, Reason, AdminUsername)
                VALUES (@UserId, @Action, @Reason, @AdminUsername)";
            AddParameter(command, "@UserId", userId);
            AddParameter(command, "@Action", action);
            AddParameter(command, "@Reason", (object?)reason ?? DBNull.Value);
            AddParameter(command, "@AdminUsername", adminUsername);
            command.ExecuteNonQuery();
        }

        // Trả về cả TargetUsername (JOIN Users) để UI hiển thị luôn tên người chơi.
        public List<BanLogEntity> GetAll(string? searchUsername = null)
        {
            var list = new List<BanLogEntity>();
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();

            var sql = @"
                SELECT bl.Id, bl.UserId, bl.Action, bl.Reason, bl.AdminUsername,
                       bl.CreatedAt, u.Username
                FROM BanLogs bl
                INNER JOIN Users u ON u.Id = bl.UserId";

            if (!string.IsNullOrWhiteSpace(searchUsername))
                sql += " WHERE u.Username LIKE @Search";

            sql += " ORDER BY bl.CreatedAt DESC, bl.Id DESC";
            command.CommandText = sql;

            if (!string.IsNullOrWhiteSpace(searchUsername))
                AddParameter(command, "@Search", "%" + searchUsername.Trim() + "%");

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new BanLogEntity
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    Action = reader.GetString(2),
                    Reason = reader.IsDBNull(3) ? null : reader.GetString(3),
                    AdminUsername = reader.GetString(4),
                    CreatedAt = reader.GetDateTime(5),
                    TargetUsername = reader.IsDBNull(6) ? null : reader.GetString(6),
                });
            }
            return list;
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