using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Services.Player.Multiplayer;
using KnapsackChallenge.Data.Repositories;

namespace KnapsackChallenge.Server.Services.Rooms
{
    // Nạp set info + items từ DB. Trả null nếu set không tồn tại hoặc rỗng.
    public sealed class RepositorySetInfoProvider : ISetInfoProvider
    {
        private readonly GameRepository _gameRepo;

        public RepositorySetInfoProvider(GameRepository gameRepo)
        {
            _gameRepo = gameRepo;
        }

        public SetInfo? Load(int setId)
        {
            var set = _gameRepo.GetSetById(setId);
            if (set == null) return null;

            var items = _gameRepo.GetItemsInSet(setId);
            if (items.Count == 0) return null;

            var itemDtos = items.Select(i => new ItemDto
            {
                Id = i.Id,
                Name = i.Name,
                Weight = i.Weight,
                Value = i.Value,
            }).ToList();

            return new SetInfo(set.Id, set.SetName, set.Difficulty, set.MaxWeight, itemDtos);
        }
    }
}