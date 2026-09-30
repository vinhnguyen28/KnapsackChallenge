using KnapsackChallenge.Data.Entities;

namespace KnapsackChallenge.Core.Services
{
    public interface IAuthService
    {
        // Hàm Login nhận vào user/pass, trả về thông tin User nếu đúng, trả về null nếu sai
        UserEntity Login(string username, string password);

        // Đăng ký tài khoản mới (luôn có Role = Player).
        // Trả về (Success, Message): Message là lý do lỗi nếu Success = false
        (bool Success, string Message) Register(string username, string password);
    }
}
