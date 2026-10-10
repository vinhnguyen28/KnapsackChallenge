using KnapsackChallenge.Core.Services.Admin;
using KnapsackChallenge.Core.Services.Auth;
using KnapsackChallenge.Core.Services.Player;
using KnapsackChallenge.Data.Repositories;

namespace KnapsackChallenge.Core.Factories
{
    public static class ServiceFactory
    {
        private static readonly Lazy<UserRepository> _userRepository = new(() => new UserRepository());
        private static readonly Lazy<ItemRepository> _itemRepository = new(() => new ItemRepository());
        private static readonly Lazy<SetRepository> _setRepository = new(() => new SetRepository());
        private static readonly Lazy<HistoryRepository> _historyRepository = new(() => new HistoryRepository());
        private static readonly Lazy<BanLogRepository> _banLogRepository = new(() => new BanLogRepository());
        private static readonly Lazy<GameRepository> _gameRepository = new(() => new GameRepository());
        private static readonly Lazy<StatsRepository> _statsRepository = new(() => new StatsRepository());
        private static readonly Lazy<GameModeRepository> _gameModeRepository = new(() => new GameModeRepository());

        // v7: HeartService là stateless (chỉ giữ 2 repo), tạo 1 lần là đủ.
        private static readonly Lazy<IHeartService> _heartService =
            new(() => new HeartService(_userRepository.Value, _gameModeRepository.Value));

        public static IAuthService GetAuthService() => new AuthService(_userRepository.Value);

        public static IItemService GetItemService() => new ItemService(_itemRepository.Value);

        public static IAdminService GetAdminService() =>
            new AdminService(_userRepository.Value, _banLogRepository.Value, _historyRepository.Value);

        public static ISetService GetSetService() => new SetService(_setRepository.Value);

        public static IPlayerSessionService GetPlayerSessionService() =>
            new PlayerSessionService(_userRepository.Value, _heartService.Value);

        public static IHeartService GetHeartService() => _heartService.Value;

        // Solo: inject thêm GameModeRepository + HeartService.
        public static ISoloGameService GetSoloGameService() =>
            new SoloGameService(_gameRepository.Value,
                                 _historyRepository.Value,
                                 _gameModeRepository.Value,
                                 _heartService.Value);

        public static IAdminStatsService GetAdminStatsService() =>
            new AdminStatsService(_statsRepository.Value);

        public static IGameModeService GetGameModeService() =>
            new GameModeService(_gameModeRepository.Value);
    }
}