using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.Data.Repositories;

namespace KnapsackChallenge.Core.Services.Admin
{
    public class ItemService : IItemService
    {
        private readonly ItemRepository _itemRepository;

        public ItemService(ItemRepository itemRepository)
        {
            _itemRepository = itemRepository;
        }

        public List<ItemEntity> GetAll() => _itemRepository.GetAll();

        public (bool Success, string Message) Add(string name, int weight, int value)
        {
            name = (name ?? "").Trim();
            var error = Validate(name, weight, value);

            if (error != null)
            { 
                return (false, error);
            }

            _itemRepository.Create(name, weight, value);

            return (true, "Đã thêm vật phẩm mới!");
        }

        public (bool Success, string Message) Update(int id, string name, int weight, int value)
        {
            var error = Validate(name, weight, value);
            if (error != null) return (false, error);

            return _itemRepository.Update(id, name, weight, value)
                ? (true, "Đã cập nhật vật phẩm!")
                : (false, "Không tìm thấy vật phẩm (có thể đã bị xóa).");
        }

        public (bool Success, string Message) Delete(int id)
        {
            return _itemRepository.Delete(id)
                ? (true, "Đã xóa vật phẩm!")
                : (false, "Không xóa được: vật phẩm đang nằm trong một bộ đề hoặc ván chơi.");
        }

        // Nghiệp vụ kiểm tra dữ liệu nằm ở Core, không nằm ở UI
        private static string? Validate(string name, int weight, int value)
        {

            if (name.Length == 0 || name.Length > 100) 
            {
                return "Tên vật phẩm phải từ 1 đến 100 ký tự!";
            }

            if (weight <= 0) 
            {
                return "Khối lượng phải lớn hơn 0!";
            }

            if (value <= 0) 
            {
                return "Giá trị phải lớn hơn 0!";
            }
          
            return null;
        }
    }
}