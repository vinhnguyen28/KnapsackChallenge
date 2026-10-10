using KnapsackChallenge.Common.DTOs;

namespace KnapsackChallenge.Core.Services.Player
{
    // Nghiệp vụ tim: tự hồi theo thời gian + tiêu tốn khi bắt đầu ván Solo.
    // Không tin số liệu từ UI — luôn đọc/ghi DB qua UserRepository.
    public interface IHeartService
    {
        // Đọc trạng thái tim hiện tại. TỰ ĐỘNG hồi tim nếu đã đủ thời gian.
        // Ghi DB nếu có thay đổi.
        HeartStatusDto GetStatus(int userId);

        // Hồi (nếu cần) rồi trừ 1 tim. Trả Success=false nếu không đủ tim.
        // Status trả về là trạng thái SAU khi trừ (dù thành công hay không).
        (bool Success, HeartStatusDto Status) TryConsume(int userId);
    }
}