using System.Data;
using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Data.Entities;

namespace KnapsackChallenge.Data.Repositories
{
    // Repository cho chế độ chơi Solo.
    // Chỉ dùng parameter, KHÔNG nối chuỗi SQL, KHÔNG dùng Dapper/EF.
    public class GameRepository
    {
        private readonly DbConnectionHelper _dbHelper = new();

        // =========================================================
        // 1. Đọc bộ đề
        // =========================================================

        // Chỉ trả bộ đề có ít nhất 1 vật phẩm.
        public List<SoloGameSetDto> GetAvailableSets()
        {
            var list = new List<SoloGameSetDto>();
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT ks.Id, ks.SetName, ks.MaxWeight, ks.Difficulty,
                       (SELECT COUNT(*) FROM SetItems si WHERE si.SetId = ks.Id) AS ItemCount
                FROM KnapsackSets ks
                WHERE EXISTS (SELECT 1 FROM SetItems si WHERE si.SetId = ks.Id)
                ORDER BY ks.Id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new SoloGameSetDto
                {
                    SetId = reader.GetInt32(0),
                    SetName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    MaxWeight = reader.GetInt32(2),
                    Difficulty = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    ItemCount = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                });
            }
            return list;
        }

        public KnapsackSetEntity? GetSetById(int setId)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT Id, SetName, MaxWeight, Difficulty FROM KnapsackSets WHERE Id = @Id";
            AddParameter(command, "@Id", setId);
            using var reader = command.ExecuteReader();
            if (!reader.Read()) return null;
            return new KnapsackSetEntity
            {
                Id = reader.GetInt32(0),
                SetName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                MaxWeight = reader.GetInt32(2),
                Difficulty = reader.IsDBNull(3) ? "" : reader.GetString(3),
            };
        }

        // Danh sách vật phẩm trong 1 bộ đề.
        public List<ItemEntity> GetItemsInSet(int setId)
        {
            var list = new List<ItemEntity>();
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT i.Id, i.Name, i.Weight, i.Value
                FROM SetItems si
                INNER JOIN Items i ON i.Id = si.ItemId
                WHERE si.SetId = @SetId
                ORDER BY i.Id";
            AddParameter(command, "@SetId", setId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ItemEntity
                {
                    Id = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Weight = reader.GetInt32(2),
                    Value = reader.GetInt32(3),
                });
            }
            return list;
        }

        // =========================================================
        // 2. Lưu 1 ván solo trong MỘT TRANSACTION
        //    Trả về SessionId vừa tạo.
        // =========================================================
        public int SaveSoloGame(int userId,
                                int setId,
                                IReadOnlyList<int> selectedItemIds,
                                int totalScore,
                                int totalWeight,
                                int optimalValue,
                                int timeSpentSeconds)
        {
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var tx = connection.BeginTransaction();

            try
            {
                // 2.1 Sinh RoomCode dạng "S-xxxxxx" duy nhất.
                string roomCode = GenerateUniqueRoomCode(connection, tx);

                // 2.2 Insert GameSessions, lấy Id vừa tạo.
                int sessionId;
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        INSERT INTO GameSessions
                            (RoomCode, Status, SetId, Mode, OptimalValue, TimeSpentSeconds)
                        OUTPUT INSERTED.Id
                        VALUES (@RoomCode, 'Finished', @SetId, 'Solo', @OptimalValue, @TimeSpentSeconds)";
                    AddParameter(cmd, "@RoomCode", roomCode);
                    AddParameter(cmd, "@SetId", setId);
                    AddParameter(cmd, "@OptimalValue", optimalValue);
                    AddParameter(cmd, "@TimeSpentSeconds", timeSpentSeconds);
                    sessionId = (int)cmd.ExecuteScalar()!;
                }

                // 2.3 Insert RoomPlayers (đã nộp).
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        INSERT INTO RoomPlayers (SessionId, UserId, TotalScore, TotalWeight, IsSubmitted)
                        VALUES (@SessionId, @UserId, @TotalScore, @TotalWeight, 1)";
                    AddParameter(cmd, "@SessionId", sessionId);
                    AddParameter(cmd, "@UserId", userId);
                    AddParameter(cmd, "@TotalScore", totalScore);
                    AddParameter(cmd, "@TotalWeight", totalWeight);
                    cmd.ExecuteNonQuery();
                }

                // 2.4 Insert SelectedItems.
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
                return sessionId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // =========================================================
        // 3. Bảng xếp hạng
        //    - Lấy điểm cao nhất của MỖI người (rn = 1).
        //    - setFilter = null: toàn bộ; ngược lại lọc theo set.
        // =========================================================
        public List<LeaderboardEntryDto> GetLeaderboard(int? setId, int topN = 20)
        {
            var list = new List<LeaderboardEntryDto>();
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();

            // Lưu ý: @SetId NULL = toàn bộ.
            command.CommandText = @"
                WITH Ranked AS (
                    SELECT rp.UserId,
                           u.Username,
                           rp.TotalScore,
                           rp.TotalWeight,
                           gs.OptimalValue,
                           gs.SetId,
                           gs.CreatedAt,
                           ks.SetName,
                           ROW_NUMBER() OVER (
                               PARTITION BY rp.UserId
                               ORDER BY rp.TotalScore DESC, gs.CreatedAt ASC, gs.Id ASC
                           ) AS rn
                    FROM RoomPlayers rp
                    INNER JOIN GameSessions gs ON gs.Id = rp.SessionId
                    INNER JOIN Users u ON u.Id = rp.UserId
                    LEFT  JOIN KnapsackSets ks ON ks.Id = gs.SetId
                    WHERE rp.IsSubmitted = 1
                      AND gs.Mode = 'Solo'
                      AND (@SetId IS NULL OR gs.SetId = @SetId)
                )
                SELECT TOP (@TopN)
                       UserId, Username, TotalScore, TotalWeight,
                       OptimalValue, SetId, SetName, CreatedAt
                FROM Ranked
                WHERE rn = 1
                ORDER BY TotalScore DESC, CreatedAt ASC";

            AddParameter(command, "@SetId", (object?)setId ?? DBNull.Value);
            AddParameter(command, "@TopN", topN);

            using var reader = command.ExecuteReader();
            int rank = 1;
            while (reader.Read())
            {
                int score = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                int optimalValue = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
                double percent = optimalValue > 0 ? (double)score / optimalValue * 100.0 : 0;

                list.Add(new LeaderboardEntryDto
                {
                    Rank = rank++,
                    UserId = reader.GetInt32(0),
                    Username = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    BestScore = score,
                    BestWeight = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    OptimalValue = optimalValue,
                    OptimalPercent = percent,
                    SetId = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    SetName = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    AchievedAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                });
            }
            return list;
        }

        // =========================================================
        // Helpers
        // =========================================================

        // Sinh RoomCode "S-xxxxxx" (không nhầm lẫn 0/O, 1/I).
        private static string GenerateUniqueRoomCode(IDbConnection connection, IDbTransaction tx)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var rnd = Random.Shared;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                var code = "S-" + new string(Enumerable.Range(0, 6)
                    .Select(_ => chars[rnd.Next(chars.Length)]).ToArray());

                using var check = connection.CreateCommand();
                check.Transaction = tx;
                check.CommandText = "SELECT COUNT(*) FROM GameSessions WHERE RoomCode = @Code";
                AddParameter(check, "@Code", code);
                int count = Convert.ToInt32(check.ExecuteScalar());
                if (count == 0) return code;
            }

            // Fallback: gần như không bao giờ xảy ra.
            return "S-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
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