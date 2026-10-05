using System.Data;
using KnapsackChallenge.Common.DTOs;

namespace KnapsackChallenge.Data.Repositories
{
    public class HistoryRepository
    {
        private readonly DbConnectionHelper _dbHelper = new();

        // N ván gần đây nhất của 1 người chơi
        public List<GameHistoryDto> GetRecentGames(int userId, int limit = 20)
        {
            var list = new List<GameHistoryDto>();
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT TOP (@Limit)
                    gs.Id, gs.RoomCode, ks.SetName, ks.Difficulty, ks.MaxWeight,
                    rp.TotalScore, rp.TotalWeight, rp.IsSubmitted, gs.CreatedAt
                FROM RoomPlayers rp
                INNER JOIN GameSessions gs  ON gs.Id = rp.SessionId
                LEFT  JOIN KnapsackSets ks  ON ks.Id = gs.SetId
                WHERE rp.UserId = @UserId
                ORDER BY gs.CreatedAt DESC, gs.Id DESC";
            AddParameter(command, "@UserId", userId);
            AddParameter(command, "@Limit", limit);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new GameHistoryDto
                {
                    SessionId = reader.GetInt32(0),
                    RoomCode = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    SetName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Difficulty = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    MaxWeight = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    TotalScore = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    TotalWeight = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                    IsSubmitted = !reader.IsDBNull(7) && reader.GetBoolean(7),
                    CreatedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                });
            }
            return list;
        }

        // Tổng hợp: tổng số ván / đã nộp / điểm cao nhất / điểm TB / tổng điểm.
        // Nếu người chơi chưa có ván nào thì tất cả = 0 (ISNULL để không phải check DBNull nhiều).
        public (int TotalGames, int SubmittedGames, int HighestScore, double AverageScore, int TotalScore)
            GetSummary(int userId)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT
                    COUNT(*)                                                    AS TotalGames,
                    ISNULL(SUM(CASE WHEN IsSubmitted = 1 THEN 1 ELSE 0 END), 0) AS SubmittedGames,
                    ISNULL(MAX(TotalScore), 0)                                  AS HighestScore,
                    ISNULL(AVG(CAST(TotalScore AS FLOAT)), 0)                   AS AverageScore,
                    ISNULL(SUM(TotalScore), 0)                                  AS TotalScore
                FROM RoomPlayers
                WHERE UserId = @UserId";
            AddParameter(command, "@UserId", userId);

            using var reader = command.ExecuteReader();
            if (!reader.Read()) return (0, 0, 0, 0, 0);

            return (
                TotalGames: Convert.ToInt32(reader.GetValue(0)),
                SubmittedGames: Convert.ToInt32(reader.GetValue(1)),
                HighestScore: Convert.ToInt32(reader.GetValue(2)),
                AverageScore: Convert.ToDouble(reader.GetValue(3)),
                TotalScore: Convert.ToInt32(reader.GetValue(4))
            );
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