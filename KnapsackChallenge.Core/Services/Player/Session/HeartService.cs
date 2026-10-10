using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Data.Repositories;

namespace KnapsackChallenge.Core.Services.Player
{
    public class HeartService : IHeartService
    {
        // Fallback khi GameModes.Solo không có cấu hình.
        private const int DefaultMaxHearts = 25;
        private const int DefaultRefillMinutes = 30;

        private readonly UserRepository _userRepo;
        private readonly GameModeRepository _modeRepo;

        public HeartService(UserRepository userRepo, GameModeRepository modeRepo)
        {
            _userRepo = userRepo;
            _modeRepo = modeRepo;
        }

        public HeartStatusDto GetStatus(int userId)
        {
            var (hearts, baseTime, max, refillMin) = LoadAndRefill(userId);
            DateTime? nextRefill = hearts >= max
                ? (DateTime?)null
                : (baseTime ?? DateTime.UtcNow).AddMinutes(refillMin);
            return new HeartStatusDto(hearts, max, nextRefill, refillMin);
        }

        public (bool Success, HeartStatusDto Status) TryConsume(int userId)
        {
            var (hearts, baseTime, max, refillMin) = LoadAndRefill(userId);

            if (hearts <= 0)
            {
                DateTime? nextWhenEmpty = (baseTime ?? DateTime.UtcNow).AddMinutes(refillMin);
                return (false, new HeartStatusDto(hearts, max, nextWhenEmpty, refillMin));
            }

            int newHearts = hearts - 1;

            // Nếu vừa từ mức max xuống dưới, BẮT ĐẦU đếm lại từ bây giờ.
            // Nếu đã dưới max từ trước, giữ nguyên mốc base (không reset công sức chờ).
            DateTime newBase = (hearts >= max)
                ? DateTime.UtcNow
                : (baseTime ?? DateTime.UtcNow);

            _userRepo.UpdateHearts(userId, newHearts, newBase);

            DateTime? next = newHearts >= max
                ? (DateTime?)null
                : newBase.AddMinutes(refillMin);

            return (true, new HeartStatusDto(newHearts, max, next, refillMin));
        }

        // =========================================================
        // Helper: đọc DB + tính toán hồi tim + ghi DB nếu thay đổi.
        // Trả về: (hearts hiện tại, mốc base, max, refillMin).
        //   - base = mốc bắt đầu đếm cho tim kế tiếp.
        //   - Khi hearts >= max: base không quan trọng, chỉ giữ giá trị hợp lệ.
        // =========================================================
        private (int Hearts, DateTime? Base, int Max, int RefillMin) LoadAndRefill(int userId)
        {
            var (hearts, lastRefill) = _userRepo.GetHearts(userId);

            var mode = _modeRepo.GetByKey("Solo");
            int max = mode?.MaxHearts ?? DefaultMaxHearts;
            int refillMin = mode?.HeartRefillMinutes ?? DefaultRefillMinutes;
            if (max < 1) max = 1;
            if (refillMin < 1) refillMin = DefaultRefillMinutes;

            var now = DateTime.UtcNow;

            // Trường hợp 1: đã max — đảm bảo base có giá trị (không cần chính xác).
            if (hearts >= max)
            {
                if (lastRefill == null)
                    _userRepo.UpdateHearts(userId, hearts, now);
                return (hearts, lastRefill ?? now, max, refillMin);
            }

            // Trường hợp 2: dưới max mà chưa có base -> gán base = now (bắt đầu đếm).
            if (lastRefill == null)
            {
                _userRepo.UpdateHearts(userId, hearts, now);
                return (hearts, now, max, refillMin);
            }

            // Trường hợp 3: tính số tim hồi được từ base.
            var elapsed = now - lastRefill.Value;
            if (elapsed.TotalMinutes < refillMin)
                return (hearts, lastRefill, max, refillMin);

            int toAdd = (int)(elapsed.TotalMinutes / refillMin);
            int newHearts = Math.Min(max, hearts + toAdd);
            var newBase = lastRefill.Value.AddMinutes((long)toAdd * refillMin);

            if (newHearts >= max)
            {
                // Vừa max: reset base = now để khi tiêu tốn tiếp thì đếm lại từ đầu.
                _userRepo.UpdateHearts(userId, newHearts, now);
                return (newHearts, now, max, refillMin);
            }

            _userRepo.UpdateHearts(userId, newHearts, newBase);
            return (newHearts, newBase, max, refillMin);
        }
    }
}