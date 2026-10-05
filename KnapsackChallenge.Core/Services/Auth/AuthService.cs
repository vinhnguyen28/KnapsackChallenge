using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.Data.Repositories;

namespace KnapsackChallenge.Core.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly UserRepository _userRepository;

        public AuthService(UserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        // Trả về UserEntity nếu đúng mật khẩu và KHÔNG bị ban.
        // - Sai user/pass  -> null
        // - Tài khoản bị ban -> ném AccountBannedException (kèm lý do)
        public UserEntity? Login(string username, string password)
        {
            var user = _userRepository.GetUserByUsername(username);
            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
                return null;

            bool passwordOk;
            try
            {
                passwordOk = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            }
            catch (BCrypt.Net.SaltParseException)
            {
                // Hash trong DB không đúng định dạng BCrypt
                return null;
            }

            if (!passwordOk) return null;

            // Chỉ tiết lộ trạng thái ban SAU khi đã xác thực mật khẩu.
            if (user.IsBanned)
            {
                var reason = string.IsNullOrWhiteSpace(user.BanReason)
                    ? "(không có lý do)"
                    : user.BanReason;
                throw new AccountBannedException($"Tài khoản của bạn đã bị khóa. Lý do: {reason}");
            }

            // Heartbeat: đánh dấu vừa đăng nhập
            _userRepository.MarkLogin(user.Id);

            // Đồng bộ field mới nhất cho caller (LastLoginAt/LastSeenAt đã đổi trong DB)
            user.LastLoginAt = DateTime.UtcNow;
            user.LastSeenAt = DateTime.UtcNow;
            return user;
        }

        public (bool Success, string Message) Register(string username, string password)
        {
            username = (username ?? "").Trim();

            if (username.Length < 3 || username.Length > 50)
                return (false, "Tài khoản phải từ 3 đến 50 ký tự!");

            if (string.IsNullOrEmpty(password) || password.Length < 3)
                return (false, "Mật khẩu phải có ít nhất 3 ký tự!");

            if (_userRepository.GetUserByUsername(username) != null)
                return (false, "Tài khoản đã tồn tại, hãy chọn tên khác!");

            string hash = BCrypt.Net.BCrypt.HashPassword(password);
            bool created = _userRepository.CreateUser(username, hash, "Player");

            return created
                ? (true, "")
                : (false, "Tài khoản đã tồn tại, hãy chọn tên khác!");
        }
    }
}