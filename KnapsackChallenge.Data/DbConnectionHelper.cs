using Microsoft.Data.SqlClient;
using System.Data;

namespace KnapsackChallenge.Data
{
    public class DbConnectionHelper
    {
        // Chuỗi kết nối tới SQL Server (Sửa "localhost" thành tên server SQL của bạn nếu cần)
        private readonly string _connectionString = @"Server=192.168.104.1;Database=KnapsackChallenge;User Id=sa;Password=Vq112113;TrustServerCertificate=True;";

        public IDbConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}

