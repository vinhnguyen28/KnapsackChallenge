using KnapsackChallenge.Data.Entities;

namespace KnapsackChallenge.Core.Services.Admin
{
    public interface IItemService
    {
        List<ItemEntity> GetAll();

        // v10: thêm iconKey (null = icon "default"). Tham số có default null
        // để code cũ gọi Add(name, weight, value) vẫn biên dịch được.
        (bool Success, string Message) Add(string name, int weight, int value, string? iconKey = null);

        // v10: tương tự cho Update.
        (bool Success, string Message) Update(int id, string name, int weight, int value, string? iconKey = null);

        (bool Success, string Message) Delete(int id);
    }
}