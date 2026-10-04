using KnapsackChallenge.Data.Entities;

namespace KnapsackChallenge.Core.Services.Admin
{
    public interface IItemService
    {
        List<ItemEntity> GetAll();
        (bool Success, string Message) Add(string name, int weight, int value);
        (bool Success, string Message) Update(int id, string name, int weight, int value);
        (bool Success, string Message) Delete(int id);
    }
}