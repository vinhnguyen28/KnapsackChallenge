using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace KnapsackChallenge.Data
{
    public class DbConnectionHelper
    {
        // Đọc cấu hình LAZY: chỉ đọc khi thật sự mở kết nối.
        // Nhờ vậy thiếu file/connection string sẽ ném lỗi bên trong try/catch của ViewModel,
        // thay vì làm app sập ngay lúc khởi tạo (TypeInitializationException).
        private static readonly Lazy<string> _connectionString = new(BuildConnectionString);

        private static string BuildConnectionString()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Local.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var raw = config.GetConnectionString("KnapsackDb")
                ?? throw new InvalidOperationException(
                    "Thiếu ConnectionStrings:KnapsackDb trong appsettings.json.");

            // Mặc định timeout là 15s -> UI đơ lâu khi server không truy cập được. Giảm còn 5s
            // (chỉ áp dụng nếu chuỗi kết nối chưa tự đặt).
            var builder = new SqlConnectionStringBuilder(raw);
            if (!raw.Contains("Connect Timeout", StringComparison.OrdinalIgnoreCase) &&
                !raw.Contains("Connection Timeout", StringComparison.OrdinalIgnoreCase))
            {
                builder.ConnectTimeout = 5;
            }
            return builder.ConnectionString;
        }

        public IDbConnection CreateConnection() => new SqlConnection(_connectionString.Value);
    }
}