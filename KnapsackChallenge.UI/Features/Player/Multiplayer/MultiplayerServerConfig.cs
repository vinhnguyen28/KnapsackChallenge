using Microsoft.Extensions.Configuration;

namespace KnapsackChallenge.UI.Features.Player
{
    // Đọc Multiplayer:ServerUrl LAZY (giống DbConnectionHelper).
    // Nếu thiếu config -> fallback localhost, KHÔNG làm app crash.
    internal static class MultiplayerServerConfig
    {
        private const string DefaultUrl = "http://localhost:5208";

        private static readonly Lazy<string> _serverUrl = new(BuildServerUrl);

        public static string ServerUrl => _serverUrl.Value;

        private static string BuildServerUrl()
        {
            try
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddJsonFile("appsettings.Local.json", optional: true)
                    .AddEnvironmentVariables()
                    .Build();

                var raw = config["Multiplayer:ServerUrl"];
                if (string.IsNullOrWhiteSpace(raw)) return DefaultUrl;
                return raw.TrimEnd('/');
            }
            catch
            {
                return DefaultUrl;
            }
        }
    }
}