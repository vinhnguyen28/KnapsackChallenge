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

        public static IAuthService GetAuthService() => new AuthService(_userRepository.Value);

        public static IItemService GetItemService() => new ItemService(_itemRepository.Value);

        public static IAdminService GetAdminService() =>
            new AdminService(_userRepository.Value, _banLogRepository.Value, _historyRepository.Value);

        public static ISetService GetSetService() =>
            new SetService(_setRepository.Value);

        public static IPlayerSessionService GetPlayerSessionService() =>
            new PlayerSessionService(_userRepository.Value);
    }
}