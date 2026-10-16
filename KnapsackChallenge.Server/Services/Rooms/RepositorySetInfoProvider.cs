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

            // v10: map thêm IconKey để client render icon trong phòng Multiplayer.
            var itemDtos = items.Select(i => new ItemDto
            {
                Id = i.Id,
                Name = i.Name,
                Weight = i.Weight,
                Value = i.Value,
                IconKey = i.IconKey, // v10
            }).ToList();

            return new SetInfo(set.Id, set.SetName, set.Difficulty, set.MaxWeight, itemDtos);
        }

        public SetInfo? LoadRandomByDifficulty(string difficulty)
        {
            if (string.IsNullOrWhiteSpace(difficulty)) return null;

            var all = _gameRepo.GetAvailableSets();
            var matches = all
                .Where(s => string.Equals(s.Difficulty, difficulty, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count == 0) return null;

            int idx = Random.Shared.Next(matches.Count);
            return Load(matches[idx].SetId);
        }
    }
}