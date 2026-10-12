using KnapsackChallenge.Core.Services.Admin;
using KnapsackChallenge.Core.Services.Auth;
using KnapsackChallenge.Core.Services.Player;
using KnapsackChallenge.UI.Services.Remote;

namespace KnapsackChallenge.UI.Services
{
    // Bản sao UI của Core.Factories.ServiceFactory.
    // - Đúng tên + kiểu trả về của từng method public bên Core.
    // - GetAuthService -> RemoteAuthService (gọi REST Server).
    // - Các service khác TẠM THỜI gọi thẳng Core (TODO remote).
    public static class ClientServiceFactory
    {
        // ĐÃ CHUYỂN SANG REST.
        public static IAuthService GetAuthService() => new RemoteAuthService();

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
        public static IPlayerSessionService GetPlayerSessionService() =>
            KnapsackChallenge.Core.Factories.ServiceFactory.GetPlayerSessionService();

        // TODO remote.
        public static IHeartService GetHeartService() =>
            KnapsackChallenge.Core.Factories.ServiceFactory.GetHeartService();

        // TODO remote.
        public static ISoloGameService GetSoloGameService() =>
            KnapsackChallenge.Core.Factories.ServiceFactory.GetSoloGameService();

        // TODO remote.
        public static IAdminStatsService GetAdminStatsService() =>
            KnapsackChallenge.Core.Factories.ServiceFactory.GetAdminStatsService();

        // TODO remote.
        public static IGameModeService GetGameModeService() =>
            KnapsackChallenge.Core.Factories.ServiceFactory.GetGameModeService();

        // TODO remote.
        public static IRankService GetRankService() =>
            KnapsackChallenge.Core.Factories.ServiceFactory.GetRankService();

        // TODO remote.
        public static ILeaderboardService GetLeaderboardService() =>
            KnapsackChallenge.Core.Factories.ServiceFactory.GetLeaderboardService();
    }
}