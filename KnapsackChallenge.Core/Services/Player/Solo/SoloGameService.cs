using KnapsackChallenge.Common.Constants;
using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Algorithms;
using KnapsackChallenge.Data.Repositories;

namespace KnapsackChallenge.Core.Services.Player
{
    public class SoloGameService : ISoloGameService
    {
        private const int TimeToleranceSeconds = 5;

        private readonly GameRepository _gameRepository;
        private readonly HistoryRepository _historyRepository;
        private readonly GameModeRepository _gameModeRepository;
        private readonly IHeartService _heartService;

        public SoloGameService(GameRepository gameRepository,
                               HistoryRepository historyRepository,
                               GameModeRepository gameModeRepository,
                               IHeartService heartService,
                               IRankService rankService)

        {
            _gameRepository = gameRepository;
            _historyRepository = historyRepository;
            _gameModeRepository = gameModeRepository;
            _heartService = heartService;
            _rankService = rankService;
        }

        public (bool IsEnabled, int TimeLimitSeconds) GetSoloModeStatus()
        {
            var mode = _gameModeRepository.GetByKey("Solo");
            if (mode == null) return (false, 0);
            return (mode.IsEnabled, mode.TimeLimitSeconds);
        }

        public List<SoloGameSetDto> GetAvailableSets() => _gameRepository.GetAvailableSets();

        public (bool Success, string Message, SoloGameDataDto? Data, HeartStatusDto? HeartStatus)
            StartGame(int userId, int setId)
        {
            // v4: chặn khi chế độ Solo đang tắt.
            var mode = _gameModeRepository.GetByKey("Solo");
            if (mode == null || !mode.IsEnabled)
                return (false, "Chế độ này đang tạm đóng.", null, null);

            // v7: kiểm tra + tiêu tốn 1 tim TRƯỚC khi nạp bộ đề.
            // Nếu bộ đề hỏng thì ta đã lỡ mất tim -> nên kiểm tra bộ đề trước.
            var set = _gameRepository.GetSetById(setId);
            if (set == null)
                return (false, "Bộ đề không tồn tại.", null, _heartService.GetStatus(userId));

            var items = _gameRepository.GetItemsInSet(setId);
            if (items.Count == 0)
                return (false, "Bộ đề không có vật phẩm.", null, _heartService.GetStatus(userId));

            // Bộ đề OK -> tiêu tốn tim.
            var (ok, heartStatus) = _heartService.TryConsume(userId);
            if (!ok)
            {
                string wait = heartStatus.CountdownText;
                return (false,
                        string.Format(Messages.HeartNotEnoughFmt, wait),
                        null,
                        heartStatus);
            }

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

            return (true, "", data, heartStatus);
        }

        public (bool Success, string Message, SoloResultDto? Result) Submit(
            int userId,
            int setId,
            IEnumerable<int> selectedItemIds,
            int timeSpentSeconds,
            bool isTimeout = false)
        {
            var mode = _gameModeRepository.GetByKey("Solo");
            if (mode == null || !mode.IsEnabled)
                return (false, "Chế độ này đang tạm đóng.", null);

            if (mode.TimeLimitSeconds > 0 && !isTimeout)
            {
                if (timeSpentSeconds > mode.TimeLimitSeconds + TimeToleranceSeconds)
                    return (false, "Thời gian nộp bài đã vượt giới hạn của chế độ.", null);
            }

            var set = _gameRepository.GetSetById(setId);
            if (set == null) return (false, "Bộ đề không tồn tại.", null);

            var items = _gameRepository.GetItemsInSet(setId);
            if (items.Count == 0) return (false, "Bộ đề không có vật phẩm.", null);

            var itemDict = items.ToDictionary(i => i.Id);
            var distinct = selectedItemIds?.Distinct().ToList() ?? new List<int>();

            if (!isTimeout && distinct.Count == 0)
                return (false, "Bạn chưa chọn vật phẩm nào.", null);

            foreach (var id in distinct)
                if (!itemDict.ContainsKey(id))
                    return (false, $"Vật phẩm #{id} không thuộc bộ đề này.", null);

            int totalWeight = 0, totalValue = 0;
            foreach (var id in distinct)
            {
                var it = itemDict[id];
                totalWeight += it.Weight;
                totalValue += it.Value;
            }

            if (totalWeight > set.MaxWeight)
            {
                if (!isTimeout)
                    return (false,
                        $"Vượt sức chứa ({totalWeight}/{set.MaxWeight}). Hãy bỏ bớt vật phẩm.",
                        null);
                distinct.Clear();
                totalWeight = 0;
                totalValue = 0;
            }

            var tuples = items.Select(i => (i.Id, i.Weight, i.Value)).ToList();
            var (optimalValue, optimalIds) = KnapsackSolver.Solve(tuples, set.MaxWeight);

            double percent = optimalValue > 0
                ? (double)totalValue / optimalValue * 100.0
                : 0;

            int stars = ScoringRules.CalculateStars(percent);

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

                ExpGained = expAward?.ExpGained ?? 0,
                OldLevel = expAward?.OldLevel ?? 0,
                NewLevel = expAward?.NewLevel ?? 0,
                RankTitle = expAward?.RankTitle ?? "",
            };

            // v8: cộng EXP sau khi lưu ván thành công.
            ExpAwardResultDto? expAward = null;
            try
            {
                int expGain = _rankService.CalculateExpGain(
                    result.Score, result.OptimalValue, result.Stars);
                expAward = _rankService.AwardExp(userId, expGain);
            }
            catch
            {
                // Không để lỗi DB khi cộng EXP làm hỏng luồng nộp bài.
                // Ván đã được lưu thành công → vẫn trả result bình thường.
            }

            return (true, "Nộp bài thành công!", result);
        }

        public List<GameHistoryDto> GetHistory(int userId, int limit = 20)
            => _historyRepository.GetRecentGames(userId, limit);

        public List<LeaderboardEntryDto> GetLeaderboard(int? setId, int topN = 20)
            => _gameRepository.GetLeaderboard(setId, topN);
    }
}