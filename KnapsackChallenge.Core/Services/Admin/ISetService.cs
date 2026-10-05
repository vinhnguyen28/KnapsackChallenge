using KnapsackChallenge.Data.Entities;

namespace KnapsackChallenge.Core.Services.Admin
{
    public interface ISetService
    {
        List<KnapsackSetEntity> GetAll();
        (bool Success, string Message) Add(string setName, int maxWeight, string difficulty);
        (bool Success, string Message) Update(int id, string setName, int maxWeight, string difficulty);
        (bool Success, string Message) Delete(int id);

        // Bảng nối SetItems
        List<int> GetItemIdsInSet(int setId);
        (bool Success, string Message) ReplaceSetItems(int setId, IEnumerable<int> itemIds);
    }
}