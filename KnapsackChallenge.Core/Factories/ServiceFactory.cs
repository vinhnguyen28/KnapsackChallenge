using KnapsackChallenge.Core.Services.Admin;
using KnapsackChallenge.Core.Services.Auth;
using KnapsackChallenge.Data.Repositories;

namespace KnapsackChallenge.Core.Factories
{
    public static class ServiceFactory
    {
        // Repository chỉ giữ chuỗi kết nối lazily (xem DbConnectionHelper) nên tạo ở đây an toàn,
        // không còn nguy cơ TypeInitializationException làm hỏng mọi lần gọi sau đó.
        private static readonly Lazy<UserRepository> _userRepository = new(() => new UserRepository());
        private static readonly Lazy<ItemRepository> _itemRepository = new(() => new ItemRepository());

        public static IAuthService GetAuthService() => new AuthService(_userRepository.Value);

        public static IItemService GetItemService() => new ItemService(_itemRepository.Value);
    }
}