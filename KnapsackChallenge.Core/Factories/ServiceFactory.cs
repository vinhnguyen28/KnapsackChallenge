
using KnapsackChallenge.Core.Services;
using KnapsackChallenge.Data.Repositories;

namespace KnapsackChallenge.Core.Factories
{
    public static class ServiceFactory
    {
        // 1. Khởi tạo sẵn các Repository của tầng Data
        private static readonly UserRepository _userRepository = new UserRepository();
        private static readonly ItemRepository _itemRepository = new ItemRepository();
        // 2. Hàm này để tầng UI gọi tới, lấy về AuthService đã được lắp ráp sẵn
        public static IAuthService GetAuthService()
        {
            return new AuthService(_userRepository);
        }

        public static IItemService GetItemService()
        {
            return new ItemService(_itemRepository);
        }
    }
}
