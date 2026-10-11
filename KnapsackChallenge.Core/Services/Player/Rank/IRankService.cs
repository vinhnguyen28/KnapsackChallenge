using KnapsackChallenge.Common.DTOs;

namespace KnapsackChallenge.Core.Services.Player
{
    // Nghiệp vụ Rank & EXP. Không tin số liệu từ UI — luôn đọc/ghi DB qua UserRepository.
    public interface IRankService
    {
        // Đọc trạng thái hiện tại (đã tính level, progress, rank title).
        RankStatusDto GetStatus(int userId);

        // Cộng EXP sau khi nộp bài. Trả về thông tin để UI hiển thị "+XX EXP".
        // expToAdd <= 0 → trả về không đổi (không throw).
        ExpAwardResultDto AwardExp(int userId, int expToAdd);

        // Tiện ích: tính exp gain từ kết quả 1 ván. Pure, không đụng DB.
        int CalculateExpGain(int score, int optimalValue, int stars);
    }
}