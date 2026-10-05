using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace KnapsackChallenge.Data
{
    public class DbConnectionHelper
    {
        private static readonly IConfigurationRoot _config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        private readonly string _connectionString =
            _config.GetConnectionString("KnapsackDb")
            ?? throw new InvalidOperationException(
                "Thiếu ConnectionStrings:KnapsackDb trong appsettings.json.");

        public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
    }
}