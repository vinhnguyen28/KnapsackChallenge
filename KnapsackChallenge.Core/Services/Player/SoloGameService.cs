using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Algorithms;
using KnapsackChallenge.Data.Repositories;

namespace KnapsackChallenge.Core.Services.Player
{
    public class SoloGameService : ISoloGameService
    {
        // Biên dung sai thời gian khi nộp bài (giây) - tránh lỗi do trễ mạng.
        private const int TimeToleranceSeconds = 5;

        private readonly GameRepository _gameRepository;
        private readonly HistoryRepository _historyRepository;
        private readonly GameModeRepository _gameModeRepository;

        public SoloGameService(GameRepository gameRepository,
                               HistoryRepository historyRepository,
                               GameModeRepository gameModeRepository)
        {
            _gameRepository = gameRepository;
            _historyRepository = historyRepository;
            _gameModeRepository = gameModeRepository;
        }

        public (bool IsEnabled, int TimeLimitSeconds) GetSoloModeStatus()
        {
            var mode = _gameModeRepository.GetByKey("Solo");
            if (mode == null) return (false, 0);
            return (mode.IsEnabled, mode.TimeLimitSeconds);
        }

        public List<SoloGameSetDto> GetAvailableSets() => _gameRepository.GetAvailableSets();

        public (bool Success, string Message, SoloGameDataDto? Data) StartGame(int setId)
        {
            // v4: chặn khi chế độ Solo đang tắt.
            var mode = _gameModeRepository.GetByKey("Solo");
            if (mode == null || !mode.IsEnabled)
                return (false, "Chế độ này đang tạm đóng.", null);

            var set = _gameRepository.GetSetById(setId);
            if (set == null)
                return (false, "Bộ đề không tồn tại.", null);

            var items = _gameRepository.GetItemsInSet(setId);
            if (items.Count == 0)
                return (false, "Bộ đề không có vật phẩm.", null);

            var data = new SoloGameDataDto
            {
                SetId = set.Id,
                SetName = set.SetName,
                Difficulty = set.Difficulty,
                MaxWeight = set.MaxWeight,
                TimeLimitSeconds = mode.TimeLimitSeconds,
                Items = items.Select(i => new ItemDto
                {
                    Id = i.Id,
                    Name = i.Name,
                    Weight = i.Weight,
                    Value = i.Value,
                }).ToList(),
            };

            return (true, "", data);
        }

        public (bool Success, string Message, SoloResultDto? Result) Submit(
            int userId,
            int setId,
            IEnumerable<int> selectedItemIds,
            int timeSpentSeconds,
            bool isTimeout = false)
        {
            // v4: kiểm tra lại chế độ Solo (có thể Admin vừa tắt).
            var mode = _gameModeRepository.GetByKey("Solo");
            if (mode == null || !mode.IsEnabled)
                return (false, "Chế độ này đang tạm đóng.", null);

            // v4: kiểm tra thời gian vượt giới hạn (bỏ qua nếu chế độ không giới hạn).
            if (mode.TimeLimitSeconds > 0 && !isTimeout)
            {
                if (timeSpentSeconds > mode.TimeLimitSeconds + TimeToleranceSeconds)
                    return (false, "Thời gian nộp bài đã vượt giới hạn của chế độ.", null);
            }

            var set = _gameRepository.GetSetById(setId);
            if (set == null)
                return (false, "Bộ đề không tồn tại.", null);

            var items = _gameRepository.GetItemsInSet(setId);
            if (items.Count == 0)
                return (false, "Bộ đề không có vật phẩm.", null);

            var itemDict = items.ToDictionary(i => i.Id);

            var distinct = selectedItemIds?.Distinct().ToList() ?? new List<int>();

            // Nếu không phải timeout: yêu cầu phải có ít nhất 1 item.
            if (!isTimeout && distinct.Count == 0)
                return (false, "Bạn chưa chọn vật phẩm nào.", null);

            // Validate item thuộc bộ đề.
            foreach (var id in distinct)
            {
                if (!itemDict.ContainsKey(id))
                    return (false, $"Vật phẩm #{id} không thuộc bộ đề này.", null);
            }

            // Server-side tính lại KL/GT.
            int totalWeight = 0, totalValue = 0;
            foreach (var id in distinct)
            {
                var it = itemDict[id];
                totalWeight += it.Weight;
                totalValue += it.Value;
            }

            // Xử lý vượt sức chứa.
            if (totalWeight > set.MaxWeight)
            {
                if (!isTimeout)
                    return (false,
                        $"Vượt sức chứa ({totalWeight}/{set.MaxWeight}). Hãy bỏ bớt vật phẩm.",
                        null);
                // Timeout mà vượt sức chứa: tự bỏ hết -> 0 điểm.
                distinct.Clear();
                totalWeight = 0;
                totalValue = 0;
            }

            // Tính đáp án tối ưu + % + sao.
            var tuples = items.Select(i => (i.Id, i.Weight, i.Value)).ToList();
            var (optimalValue, optimalIds) = KnapsackSolver.Solve(tuples, set.MaxWeight);

            double percent = optimalValue > 0
                ? (double)totalValue / optimalValue * 100.0
                : 0;

            int stars = percent >= 100.0 ? 3
                      : percent >= 90.0 ? 2
                      : percent >= 70.0 ? 1
                      : 0;

            if (timeSpentSeconds < 0) timeSpentSeconds = 0;

            _gameRepository.SaveSoloGame(
                userId, setId, distinct,
                totalValue, totalWeight, optimalValue, timeSpentSeconds);

            var result = new SoloResultDto
            {
                Score = totalValue,
                TotalWeight = totalWeight,
                MaxWeight = set.MaxWeight,
                OptimalValue = optimalValue,
                OptimalPercent = percent,
                Stars = stars,
                OptimalItemIds = optimalIds,
            };

            return (true, "Nộp bài thành công!", result);
        }

        public List<GameHistoryDto> GetHistory(int userId, int limit = 20)
            => _historyRepository.GetRecentGames(userId, limit);

        public List<LeaderboardEntryDto> GetLeaderboard(int? setId, int topN = 20)
            => _gameRepository.GetLeaderboard(setId, topN);
    }
}