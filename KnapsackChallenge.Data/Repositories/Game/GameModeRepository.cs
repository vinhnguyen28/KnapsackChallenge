using System.Data;
using KnapsackChallenge.Data.Entities;

namespace KnapsackChallenge.Data.Repositories
{
    public class GameModeRepository
    {
        private readonly DbConnectionHelper _dbHelper = new();

        public List<GameModeEntity> GetAll()
        {
            var list = new List<GameModeEntity>();
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT ModeKey, DisplayName, IsEnabled, TimeLimitSeconds,
                       MaxPlayers, UpdatedAt, UpdatedBy
                FROM GameModes
                ORDER BY ModeKey";
            using var reader = command.ExecuteReader();
            while (reader.Read()) list.Add(Map(reader));
            return list;
        }

        public GameModeEntity? GetByKey(string modeKey)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT ModeKey, DisplayName, IsEnabled, TimeLimitSeconds,
                       MaxPlayers, UpdatedAt, UpdatedBy
                FROM GameModes
                WHERE ModeKey = @Key";
            AddParameter(command, "@Key", modeKey);
            using var reader = command.ExecuteReader();
            return reader.Read() ? Map(reader) : null;
        }

        public bool Update(string modeKey,
                           bool isEnabled,
                           int timeLimitSeconds,
                           int? maxPlayers,
                           string updatedBy)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE GameModes SET
                    IsEnabled        = @IsEnabled,
                    TimeLimitSeconds = @TimeLimit,
                    MaxPlayers       = @MaxPlayers,
                    UpdatedAt        = SYSUTCDATETIME(),
                    UpdatedBy        = @UpdatedBy
                WHERE ModeKey = @Key";
            AddParameter(command, "@Key", modeKey);
            AddParameter(command, "@IsEnabled", isEnabled);
            AddParameter(command, "@TimeLimit", timeLimitSeconds);
            AddParameter(command, "@MaxPlayers", (object?)maxPlayers ?? DBNull.Value);
            AddParameter(command, "@UpdatedBy", updatedBy);
            return command.ExecuteNonQuery() > 0;
        }

        private static GameModeEntity Map(IDataRecord r) => new GameModeEntity
        {
            ModeKey = r.GetString(0),
            DisplayName = r.IsDBNull(1) ? "" : r.GetString(1),
            IsEnabled = !r.IsDBNull(2) && r.GetBoolean(2),
            TimeLimitSeconds = r.IsDBNull(3) ? 0 : r.GetInt32(3),
            MaxPlayers = r.IsDBNull(4) ? null : r.GetInt32(4),
            UpdatedAt = r.IsDBNull(5) ? default : r.GetDateTime(5),
            UpdatedBy = r.IsDBNull(6) ? null : r.GetString(6),
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