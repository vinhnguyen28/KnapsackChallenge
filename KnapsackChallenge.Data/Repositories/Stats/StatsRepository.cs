using System.Data;
using KnapsackChallenge.Common.DTOs;

namespace KnapsackChallenge.Data.Repositories
{
    // Truy vấn tổng hợp phục vụ trang Thống kê Admin.
    // - Chỉ tính ván IsSubmitted = 1 VÀ OptimalValue IS NOT NULL AND > 0.
    // - @Mode = NULL: tất cả; ngược lại lọc theo GameSessions.Mode.
    // - @FromDate = NULL: tất cả thời gian; ngược lại lọc CreatedAt >= @FromDate.
    //
    // LƯU Ý KIỂU DỮ LIỆU:
    //   Trong T-SQL, literal 100.0 là DECIMAL, còn 100e0 là FLOAT.
    //   Để tránh InvalidCastException khi đọc bằng GetDouble(), ta:
    //     (a) dùng 100e0 trong SQL để mọi biểu thức trả về FLOAT;
    //     (b) dùng helper GetDoubleSafe() ở C# để chấp nhận cả Decimal/Float/Double.
    public class StatsRepository
    {
        private readonly DbConnectionHelper _dbHelper = new();

        // =========================================================
        // 1. Tổng quan
        // =========================================================
        public AdminStatsOverviewDto GetOverview(string? mode, DateTime? fromDate)
        {
            var dto = new AdminStatsOverviewDto();

            using var connection = _dbHelper.CreateConnection();
            connection.Open();

            // 1.1 Số liệu tổng hợp
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT
                        COUNT(*) AS TotalGames,
                        ISNULL(SUM(CASE WHEN rp.TotalScore = gs.OptimalValue THEN 1 ELSE 0 END), 0) AS OptimalGames,
                        CAST(ISNULL(AVG(CAST(rp.TotalScore AS FLOAT) * 100e0 / gs.OptimalValue), 0) AS FLOAT) AS AvgPercent,
                        ISNULL(MAX(rp.TotalScore), 0) AS MaxScore
                    FROM RoomPlayers rp
                    INNER JOIN GameSessions gs ON gs.Id = rp.SessionId
                    WHERE rp.IsSubmitted = 1
                      AND gs.OptimalValue IS NOT NULL AND gs.OptimalValue > 0
                      AND (@Mode IS NULL OR gs.Mode = @Mode)
                      AND (@FromDate IS NULL OR gs.CreatedAt >= @FromDate)";
                AddParameter(command, "@Mode", (object?)mode ?? DBNull.Value);
                AddParameter(command, "@FromDate", (object?)fromDate ?? DBNull.Value);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    dto.TotalGames = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                    dto.OptimalGames = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    dto.AvgOptimalPercent = GetDoubleSafe(reader, 2);
                    dto.MaxScore = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);

                    dto.OptimalRate = dto.TotalGames > 0
                        ? (double)dto.OptimalGames / dto.TotalGames * 100.0
                        : 0;
                }
            }

            // 1.2 Người giữ kỷ lục điểm
            if (dto.MaxScore > 0)
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT TOP 1 u.Username
                    FROM RoomPlayers rp
                    INNER JOIN GameSessions gs ON gs.Id = rp.SessionId
                    INNER JOIN Users u ON u.Id = rp.UserId
                    WHERE rp.IsSubmitted = 1
                      AND gs.OptimalValue IS NOT NULL AND gs.OptimalValue > 0
                      AND rp.TotalScore = @MaxScore
                      AND (@Mode IS NULL OR gs.Mode = @Mode)
                      AND (@FromDate IS NULL OR gs.CreatedAt >= @FromDate)
                    ORDER BY gs.CreatedAt ASC, gs.Id ASC";
                AddParameter(command, "@MaxScore", dto.MaxScore);
                AddParameter(command, "@Mode", (object?)mode ?? DBNull.Value);
                AddParameter(command, "@FromDate", (object?)fromDate ?? DBNull.Value);

                var obj = command.ExecuteScalar();
                dto.MaxScoreHolder = obj == null || obj == DBNull.Value ? null : obj.ToString();
            }

            return dto;
        }

        // =========================================================
        // 2. Thống kê theo bộ đề
        // =========================================================
        public List<SetStatsDto> GetSetStats(string? mode, DateTime? fromDate)
        {
            var list = new List<SetStatsDto>();
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT
                    ks.Id, ks.SetName, ks.Difficulty,
                    COUNT(*) AS PlayCount,
                    CAST(ISNULL(AVG(CAST(rp.TotalScore AS FLOAT) * 100e0 / gs.OptimalValue), 0) AS FLOAT) AS AvgPercent,
                    CAST(ISNULL(SUM(CASE WHEN rp.TotalScore = gs.OptimalValue THEN 1 ELSE 0 END) * 100e0
                        / NULLIF(COUNT(*), 0), 0) AS FLOAT) AS OptimalRate,
                    ISNULL(MAX(rp.TotalScore), 0) AS MaxScore,
                    (SELECT TOP 1 u.Username
                     FROM RoomPlayers rp2
                     INNER JOIN GameSessions gs2 ON gs2.Id = rp2.SessionId
                     INNER JOIN Users u ON u.Id = rp2.UserId
                     WHERE gs2.SetId = ks.Id
                       AND rp2.IsSubmitted = 1
                       AND gs2.OptimalValue IS NOT NULL AND gs2.OptimalValue > 0
                       AND (@Mode IS NULL OR gs2.Mode = @Mode)
                       AND (@FromDate IS NULL OR gs2.CreatedAt >= @FromDate)
                     ORDER BY rp2.TotalScore DESC, gs2.CreatedAt ASC) AS RecordHolder
                FROM RoomPlayers rp
                INNER JOIN GameSessions gs ON gs.Id = rp.SessionId
                INNER JOIN KnapsackSets ks ON ks.Id = gs.SetId
                WHERE rp.IsSubmitted = 1
                  AND gs.OptimalValue IS NOT NULL AND gs.OptimalValue > 0
                  AND (@Mode IS NULL OR gs.Mode = @Mode)
                  AND (@FromDate IS NULL OR gs.CreatedAt >= @FromDate)
                GROUP BY ks.Id, ks.SetName, ks.Difficulty
                ORDER BY PlayCount DESC, ks.Id ASC";
            AddParameter(command, "@Mode", (object?)mode ?? DBNull.Value);
            AddParameter(command, "@FromDate", (object?)fromDate ?? DBNull.Value);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new SetStatsDto
                {
                    SetId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    SetName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Difficulty = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    PlayCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    AvgOptimalPercent = GetDoubleSafe(reader, 4),
                    OptimalRate = GetDoubleSafe(reader, 5),
                    MaxScore = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                    RecordHolder = reader.IsDBNull(7) ? null : reader.GetString(7),
                });
            }
            return list;
        }

        // =========================================================
        // 3. Top N người chơi (tiêu chí: BestScore DESC)
        // =========================================================
        public List<TopPlayerDto> GetTopPlayers(string? mode, DateTime? fromDate, int top)
        {
            var list = new List<TopPlayerDto>();
            using var connection = _dbHelper.CreateConnection();
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT TOP (@Top)
                    u.Id, u.Username,
                    MAX(rp.TotalScore) AS BestScore,
                    CAST(ISNULL(AVG(CAST(rp.TotalScore AS FLOAT) * 100e0 / gs.OptimalValue), 0) AS FLOAT) AS AvgPercent,
                    COUNT(*) AS GamesPlayed,
                    CAST(ISNULL(SUM(CASE WHEN rp.TotalScore = gs.OptimalValue THEN 1 ELSE 0 END) * 100e0
                        / NULLIF(COUNT(*), 0), 0) AS FLOAT) AS OptimalRate
                FROM RoomPlayers rp
                INNER JOIN GameSessions gs ON gs.Id = rp.SessionId
                INNER JOIN Users u ON u.Id = rp.UserId
                WHERE rp.IsSubmitted = 1
                  AND gs.OptimalValue IS NOT NULL AND gs.OptimalValue > 0
                  AND (@Mode IS NULL OR gs.Mode = @Mode)
                  AND (@FromDate IS NULL OR gs.CreatedAt >= @FromDate)
                GROUP BY u.Id, u.Username
                ORDER BY BestScore DESC, AvgPercent DESC, u.Id ASC";
            AddParameter(command, "@Top", top);
            AddParameter(command, "@Mode", (object?)mode ?? DBNull.Value);
            AddParameter(command, "@FromDate", (object?)fromDate ?? DBNull.Value);

            using var reader = command.ExecuteReader();
            int rank = 1;
            while (reader.Read())
            {
                list.Add(new TopPlayerDto
                {
                    Rank = rank++,
                    UserId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    Username = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    BestScore = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    AvgOptimalPercent = GetDoubleSafe(reader, 3),
                    GamesPlayed = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    OptimalRate = GetDoubleSafe(reader, 5),
                });
            }
            return list;
        }

        // =========================================================
        // Helpers
        // =========================================================

        // Đọc 1 cột kiểu số về double một cách an toàn.
        // Chấp nhận FLOAT, REAL, DECIMAL, NUMERIC, INT, BIGINT, ...
        // Tránh InvalidCastException khi SQL Server trả kiểu khác double.
        private static double GetDoubleSafe(IDataRecord reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return 0d;
            var value = reader.GetValue(ordinal);
            return value switch
            {
                double d => d,
                float f => f,
                decimal m => (double)m,
                int i => i,
                long l => l,
                short s => s,
                byte b => b,
                _ => Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture),
            };
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