using KnapsackChallenge.Core.Services.Admin;
using KnapsackChallenge.Core.Services.Auth;
using KnapsackChallenge.Core.Services.Player;
using KnapsackChallenge.UI.Services.Remote;

namespace KnapsackChallenge.UI.Services
{
    // Bản sao UI của Core.Factories.ServiceFactory.
    // - Đúng tên + kiểu trả về của từng method public bên Core.
    // - GetAuthService      -> RemoteAuthService (REST).
    // - GetPlayerSession    -> RemotePlayerSessionService (REST).
    // - GetRankService      -> RemoteRankService (REST).
    // - GetSoloGameService  -> RemoteSoloGameService (REST).
    // - Các service khác TẠM THỜI gọi thẳng Core (TODO remote).
    //
    // LƯU Ý: PlayerSession + Rank + Solo dùng INSTANCE DÙNG CHUNG (Lazy singleton)
    // để cache nội bộ (vd: tim) hoạt động xuyên suốt vòng đời ứng dụng.
    public static class ClientServiceFactory
    {
        // ---- Remote instances dùng chung ----

        private static readonly Lazy<IPlayerSessionService> _playerSession =
            new(() => new RemotePlayerSessionService());

        private static readonly Lazy<IRankService> _rankService =
            new(() => new RemoteRankService());

        private static readonly Lazy<ISoloGameService> _soloGameService =
            new(() => new RemoteSoloGameService());

        // ĐÃ CHUYỂN SANG REST.
        public static IAuthService GetAuthService() => new RemoteAuthService();

        // ĐÃ CHUYỂN SANG REST — instance dùng chung để giữ cache tim.
        public static IPlayerSessionService GetPlayerSessionService() => _playerSession.Value;

        // ĐÃ CHUYỂN SANG REST — instance dùng chung.
        public static IRankService GetRankService() => _rankService.Value;

        // ĐÃ CHUYỂN SANG REST — instance dùng chung.
        public static ISoloGameService GetSoloGameService() => _soloGameService.Value;

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

        // TODO remote.
        public static IGameModeService GetGameModeService() =>
            KnapsackChallenge.Core.Factories.ServiceFactory.GetGameModeService();

        // TODO remote.
        public static ILeaderboardService GetLeaderboardService() =>
            KnapsackChallenge.Core.Factories.ServiceFactory.GetLeaderboardService();
    }
}