using KnapsackChallenge.Core.Services.Admin;
using KnapsackChallenge.Core.Services.Auth;
using KnapsackChallenge.Core.Services.Player;
using KnapsackChallenge.Data.Repositories;

namespace KnapsackChallenge.Core.Factories
{
    public static class ServiceFactory
    {
        // Repository chỉ giữ chuỗi kết nối lazily -> tạo ở đây an toàn.
        private static readonly Lazy<UserRepository> _userRepository = new(() => new UserRepository());
        private static readonly Lazy<ItemRepository> _itemRepository = new(() => new ItemRepository());
        private static readonly Lazy<SetRepository> _setRepository = new(() => new SetRepository());
        private static readonly Lazy<HistoryRepository> _historyRepository = new(() => new HistoryRepository());
        private static readonly Lazy<BanLogRepository> _banLogRepository = new(() => new BanLogRepository());
        private static readonly Lazy<GameRepository> _gameRepository = new(() => new GameRepository());

        // v4: bảng thống kê + bảng chế độ chơi.
        private static readonly Lazy<StatsRepository> _statsRepository = new(() => new StatsRepository());
        private static readonly Lazy<GameModeRepository> _gameModeRepository = new(() => new GameModeRepository());

        public static IAuthService GetAuthService() => new AuthService(_userRepository.Value);

        public static IItemService GetItemService() => new ItemService(_itemRepository.Value);

        public static IAdminService GetAdminService() =>
            new AdminService(_userRepository.Value, _banLogRepository.Value, _historyRepository.Value);

        public static ISetService GetSetService() =>
            new SetService(_setRepository.Value);

        public static IPlayerSessionService GetPlayerSessionService() =>
            new PlayerSessionService(_userRepository.Value);

        // Solo: inject thêm GameModeRepository để kiểm tra chế độ + giới hạn thời gian.
        public static ISoloGameService GetSoloGameService() =>
            new SoloGameService(_gameRepository.Value, _historyRepository.Value, _gameModeRepository.Value);

        // v4: Thống kê & Quản lý chế độ.
        public static IAdminStatsService GetAdminStatsService() =>
            new AdminStatsService(_statsRepository.Value);

        public static IGameModeService GetGameModeService() =>
            new GameModeService(_gameModeRepository.Value);
    }
}