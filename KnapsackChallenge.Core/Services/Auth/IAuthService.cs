using KnapsackChallenge.Data.Entities;

namespace KnapsackChallenge.Core.Services.Auth
{
    public interface IAuthService
    {
        // Đăng nhập.
        // - Trả về UserEntity nếu hợp lệ.
        // - Trả về null nếu sai user/pass.
        // - Ném AccountBannedException nếu tài khoản bị ban.
        UserEntity? Login(string username, string password);

        // Đăng ký tài khoản mới (Role luôn = Player).
        (bool Success, string Message) Register(string username, string password);
    }
}