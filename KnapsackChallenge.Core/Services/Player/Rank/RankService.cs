using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Algorithms;
using KnapsackChallenge.Data.Repositories;

namespace KnapsackChallenge.Core.Services.Player
{
    public class RankService : IRankService
    {
        private readonly UserRepository _userRepo;

        public RankService(UserRepository userRepo)
        {
            _userRepo = userRepo;
        }

        public RankStatusDto GetStatus(int userId)
        {
            var (exp, level) = _userRepo.GetExpAndLevel(userId);

            // Tin DB nhưng vẫn tính lại Level từ EXP để tự sửa nếu lệch.
            int computedLevel = RankRules.GetLevelFromExp(exp);
            if (computedLevel != level)
            {
                // Tự sửa Level nếu DB không đồng bộ (không phải trường hợp thường).
                _userRepo.UpdateExpAndLevel(userId, exp, computedLevel);
                level = computedLevel;
            }

            var (current, range) = RankRules.GetProgressInLevel(exp, level);

            return new RankStatusDto
            {
                TotalExp = exp,
                Level = level,
                RankTitle = RankRules.GetRankTitle(level),
                CurrentInLevel = current,
                RangeInLevel = range,
            };
        }

        public ExpAwardResultDto AwardExp(int userId, int expToAdd)
        {
            var (oldExp, oldLevel) = _userRepo.GetExpAndLevel(userId);

            if (expToAdd <= 0)
            {
                // Vẫn trả về thông tin hiện tại để UI hiển thị đúng.
                return new ExpAwardResultDto
                {
                    ExpGained = 0,
                    OldLevel = oldLevel,
                    NewLevel = oldLevel,
                    NewTotalExp = oldExp,
                    RankTitle = RankRules.GetRankTitle(oldLevel),
                };
            }

            long newExp = oldExp + expToAdd;
            int newLevel = RankRules.GetLevelFromExp(newExp);

            _userRepo.UpdateExpAndLevel(userId, newExp, newLevel);

            return new ExpAwardResultDto
            {
                ExpGained = expToAdd,
                OldLevel = oldLevel,
                NewLevel = newLevel,
                NewTotalExp = newExp,
                RankTitle = RankRules.GetRankTitle(newLevel),
            };
        }

        public int CalculateExpGain(int score, int optimalValue, int stars)
            => RankRules.CalculateExpGain(score, optimalValue, stars);
    }
}