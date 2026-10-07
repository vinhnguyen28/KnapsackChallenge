using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.Data.Repositories;

namespace KnapsackChallenge.Core.Services.Admin
{
    public class SetService : ISetService
    {
        private static readonly string[] ValidDifficulties = { "Easy", "Medium", "Hard" };
        private readonly SetRepository _setRepository;

        public SetService(SetRepository setRepository)
        {
            _setRepository = setRepository;
        }

        public List<KnapsackSetEntity> GetAll() => _setRepository.GetAll();

        public (bool Success, string Message) Add(string setName, int maxWeight, string difficulty)
        {
            var error = Validate(setName, maxWeight, difficulty);
            if (error != null) return (false, error);

            _setRepository.Create(setName.Trim(), maxWeight, difficulty);
            return (true, "Đã thêm bộ đề mới!");
        }

        public (bool Success, string Message) Update(int id, string setName, int maxWeight, string difficulty)
        {
            var error = Validate(setName, maxWeight, difficulty);
            if (error != null) return (false, error);

            return _setRepository.Update(id, setName.Trim(), maxWeight, difficulty)
                ? (true, "Đã cập nhật bộ đề!")
                : (false, "Không tìm thấy bộ đề.");
        }

        public (bool Success, string Message) Delete(int id)
        {
            return _setRepository.Delete(id)
                ? (true, "Đã xóa bộ đề!")
                : (false, "Không xóa được: bộ đề đang được dùng trong một ván chơi.");
        }

        public List<int> GetItemIdsInSet(int setId) => _setRepository.GetItemIdsInSet(setId);

        public (bool Success, string Message) ReplaceSetItems(int setId, IEnumerable<int> itemIds)
        {
            _setRepository.ReplaceSetItems(setId, itemIds);
            return (true, "Đã lưu danh sách vật phẩm cho bộ đề!");
        }

        // Validate nghiệp vụ ở Core.
        private static string? Validate(string setName, int maxWeight, string difficulty)
        {
            setName = (setName ?? "").Trim();
            if (setName.Length == 0 || setName.Length > 100)
                return "Tên bộ đề phải từ 1 đến 100 ký tự!";
            if (maxWeight <= 0)
                return "Khối lượng tối đa phải lớn hơn 0!";
            if (Array.IndexOf(ValidDifficulties, difficulty) < 0)
                return "Độ khó phải là Easy, Medium hoặc Hard!";
            return null;
        }
    }
}