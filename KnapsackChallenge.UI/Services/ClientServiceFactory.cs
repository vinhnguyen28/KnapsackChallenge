using KnapsackChallenge.Core.Services.Admin;
using KnapsackChallenge.Core.Services.Auth;
using KnapsackChallenge.Core.Services.Player;
using KnapsackChallenge.UI.Services.Remote;

namespace KnapsackChallenge.UI.Services
{
    // Bản sao UI của Core.Factories.ServiceFactory.
    // - Đúng tên + kiểu trả về của từng method public bên Core.
    // - GetAuthService            -> RemoteAuthService (REST).
    // - GetPlayerSessionService   -> RemotePlayerSessionService (REST).
    // - GetRankService            -> RemoteRankService (REST).
    // - GetSoloGameService        -> RemoteSoloGameService (REST).
    // - GetLeaderboardService     -> RemoteLeaderboardService (REST).
    // - GetGameModeService        -> RemoteGameModeService (REST).
    // - Các service khác TẠM THỜI gọi thẳng Core (TODO remote).
    //
    // LƯU Ý: các Remote instance dùng chung Lazy singleton để cache nội bộ
    // (tim, danh sách chế độ...) hoạt động xuyên suốt vòng đời ứng dụng.
    public static class ClientServiceFactory
    {
        // ---- Remote instances dùng chung ----

        private static readonly Lazy<IPlayerSessionService> _playerSession =
            new(() => new RemotePlayerSessionService());

        private static readonly Lazy<IRankService> _rankService =
            new(() => new RemoteRankService());

        private static readonly Lazy<ISoloGameService> _soloGameService =
            new(() => new RemoteSoloGameService());

        private static readonly Lazy<ILeaderboardService> _leaderboardService =
            new(() => new RemoteLeaderboardService());

        private static readonly Lazy<IGameModeService> _gameModeService =
            new(() => new RemoteGameModeService());

        // ĐÃ CHUYỂN SANG REST.
        public static IAuthService GetAuthService() => new RemoteAuthService();

        // ĐÃ CHUYỂN SANG REST — instance dùng chung để giữ cache tim.
        public static IPlayerSessionService GetPlayerSessionService() => _playerSession.Value;

        // ĐÃ CHUYỂN SANG REST — instance dùng chung.
        public static IRankService GetRankService() => _rankService.Value;

        // ĐÃ CHUYỂN SANG REST — instance dùng chung.
        public static ISoloGameService GetSoloGameService() => _soloGameService.Value;

        // ĐÃ CHUYỂN SANG REST — instance dùng chung.
        public static ILeaderboardService GetLeaderboardService() => _leaderboardService.Value;

        // ĐÃ CHUYỂN SANG REST — instance dùng chung.
        public static IGameModeService GetGameModeService() => _gameModeService.Value;

        // TODO remote: chuyển sang REST endpoint tương ứng.
        public static IItemService GetItemService() =>
            KnapsackChallenge.Core.Factories.ServiceFactory.GetItemService();

        // TODO remote.
        public static IAdminService GetAdminService() =>
            KnapsackChallenge.Core.Factories.ServiceFactory.GetAdminService();

        // TODO remote.
        public static ISetService GetSetService() =>
            KnapsackChallenge.Core.Factories.ServiceFactory.GetSetService();

        // TODO remote.
        public static IHeartService GetHeartService() =>
            KnapsackChallenge.Core.Factories.ServiceFactory.GetHeartService();

        // TODO remote.
        public static IAdminStatsService GetAdminStatsService() =>
            KnapsackChallenge.Core.Factories.ServiceFactory.GetAdminStatsService();
    }
}