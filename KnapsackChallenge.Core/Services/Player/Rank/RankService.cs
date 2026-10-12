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
                // Tự sửa Level nếu DB không đồng bộ. CHỈ ghi cột Level,
                // KHÔNG ghi đè TotalExp để tránh xoá EXP vừa cộng từ luồng khác.
                _userRepo.SetLevel(userId, computedLevel);
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
            if (expToAdd <= 0)
            {
                // Nhánh "không cộng gì" vẫn cần đọc dữ liệu để UI hiển thị đúng.
                var (expNow, levelNow) = _userRepo.GetExpAndLevel(userId);
                return new ExpAwardResultDto
                {
                    ExpGained = 0,
                    OldLevel = levelNow,
                    NewLevel = levelNow,
                    NewTotalExp = expNow,
                    RankTitle = RankRules.GetRankTitle(levelNow),
                };
            }

            // Cộng EXP nguyên tử ở DB, không đọc-rồi-ghi ở C#.
            var added = _userRepo.AddExpAtomic(userId, expToAdd);
            if (added == null)
            {
                // User không tồn tại — trả về trạng thái mặc định an toàn.
                return new ExpAwardResultDto
                {
                    ExpGained = 0,
                    OldLevel = 1,
                    NewLevel = 1,
                    NewTotalExp = 0,
                    RankTitle = RankRules.GetRankTitle(1),
                };
            }

            var (prevExp, newExp) = added.Value;
            int oldLevel = RankRules.GetLevelFromExp(prevExp);
            int newLevel = RankRules.GetLevelFromExp(newExp);

            // Level chỉ tăng: nếu luồng khác đã set Level cao hơn thì giữ nguyên.
            _userRepo.RaiseLevelTo(userId, newLevel);

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