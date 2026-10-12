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

            // Trừ tim nguyên tử ở DB: câu UPDATE có WHERE Hearts > 0 và CASE tự
            // chọn mốc hồi (đầy/chưa có mốc → now, ngược lại giữ mốc cũ).
            var res = _userRepo.TryConsumeHeart(userId, max, DateTime.UtcNow);
            if (!res.Success)
            {
                // Luồng khác vừa lấy mất tim cuối — trả về trạng thái mới nhất.
                return (false, GetStatus(userId));
            }

            DateTime? next = res.Hearts >= max
                ? (DateTime?)null
                : res.LastRefillAt?.AddMinutes(refillMin);

            return (true, new HeartStatusDto(res.Hearts, max, next, refillMin));
        }

        // =========================================================
        // Helper: đọc DB + tính toán hồi tim + ghi DB nếu thay đổi.
        // Trả về: (hearts hiện tại, mốc base, max, refillMin).
        //   - base = mốc bắt đầu đếm cho tim kế tiếp.
        //   - Khi hearts >= max: base không quan trọng, chỉ giữ giá trị hợp lệ.
        // =========================================================
        // Đọc DB + tính hồi tim + ghi lại bằng CAS.
        // Nếu CAS thất bại (luồng khác vừa sửa) → đọc lại và tính lại, tối đa 3 lần.
        // Sau 3 lần đều thất bại → trả giá trị vừa tính, KHÔNG ghi (lần gọi sau sẽ tự hồi).
        private (int Hearts, DateTime? Base, int Max, int RefillMin) LoadAndRefill(int userId)
        {
            var mode = _modeRepo.GetByKey("Solo");
            int max = mode?.MaxHearts ?? DefaultMaxHearts;
            int refillMin = mode?.HeartRefillMinutes ?? DefaultRefillMinutes;
            if (max < 1) max = 1;
            if (refillMin < 1) refillMin = DefaultRefillMinutes;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                var (hearts, lastRefill) = _userRepo.GetHearts(userId);
                var now = DateTime.UtcNow;

                // Trường hợp 1: đã max — đảm bảo base có giá trị (không cần chính xác).
                if (hearts >= max)
                {
                    if (lastRefill == null)
                    {
                        if (!_userRepo.TrySetHeartsIfUnchanged(
                                userId, hearts, lastRefill, hearts, now))
                            continue; // có luồng khác vừa sửa → đọc lại
                        return (hearts, now, max, refillMin);
                    }
                    return (hearts, lastRefill, max, refillMin);
                }

                // Trường hợp 2: dưới max mà chưa có base -> gán base = now (bắt đầu đếm).
                if (lastRefill == null)
                {
                    if (!_userRepo.TrySetHeartsIfUnchanged(
                            userId, hearts, lastRefill, hearts, now))
                        continue;
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
                    if (!_userRepo.TrySetHeartsIfUnchanged(
                            userId, hearts, lastRefill, newHearts, now))
                        continue;
                    return (newHearts, now, max, refillMin);
                }

                if (!_userRepo.TrySetHeartsIfUnchanged(
                        userId, hearts, lastRefill, newHearts, newBase))
                    continue;

                return (newHearts, newBase, max, refillMin);
            }

            // 3 lần CAS đều thất bại: đọc lại lần cuối, trả giá trị đã tính mà KHÔNG ghi.
            var (hFinal, lastFinal) = _userRepo.GetHearts(userId);
            var nowF = DateTime.UtcNow;

            if (hFinal >= max) return (hFinal, lastFinal ?? nowF, max, refillMin);
            if (lastFinal == null) return (hFinal, nowF, max, refillMin);

            var elapsedF = nowF - lastFinal.Value;
            if (elapsedF.TotalMinutes < refillMin)
                return (hFinal, lastFinal, max, refillMin);

            int toAddF = (int)(elapsedF.TotalMinutes / refillMin);
            int newHeartsF = Math.Min(max, hFinal + toAddF);
            DateTime newBaseF = newHeartsF >= max
                ? nowF
                : lastFinal.Value.AddMinutes((long)toAddF * refillMin);

            return (newHeartsF, newBaseF, max, refillMin);
        }
    }
}