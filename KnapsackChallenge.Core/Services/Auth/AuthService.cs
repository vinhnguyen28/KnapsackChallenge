using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.Data.Repositories;
namespace KnapsackChallenge.Core.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly UserRepository _userRepository;

        // Constructor nhận vào UserRepository từ Data
        public AuthService(UserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public UserEntity? Login(string username, string password)
        {
            // Bước 1: Gọi Data lấy User theo Username
            var user = _userRepository.GetUserByUsername(username);

            // Bước 2: Kiểm tra tài khoản có tồn tại không
            if (user == null || string.IsNullOrEmpty(user.PasswordHash))
            {
                return null; // Không tồn tại -> Trả về null
            }

            try
            {
                // Verify tự tách salt từ chuỗi hash rồi so sánh
                return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash) ? user : null;
            }
            catch (BCrypt.Net.SaltParseException)
            {
                // Hash trong DB không đúng định dạng BCrypt (ví dụ tài khoản cũ lưu '123')
                return null;
            }
        }

        public (bool Success, string Message) Register(string username, string password)
        {
            // Bước 1: Kiểm tra dữ liệu đầu vào (nghiệp vụ nằm ở Core, không nằm ở UI)
            username = (username ?? "").Trim();

            if (username.Length < 3 || username.Length > 50)
                return (false, "Tài khoản phải từ 3 đến 50 ký tự!");

            if (string.IsNullOrEmpty(password) || password.Length < 3) 
                return (false, "Mật khẩu phải có ít nhất 3 ký tự!");

            // Bước 2: Kiểm tra tài khoản đã tồn tại chưa
            if (_userRepository.GetUserByUsername(username) != null)
                return (false, "Tài khoản đã tồn tại, hãy chọn tên khác!");

            // Bước 3: Ghi vào DB. Role luôn là "Player" để không ai tự đăng ký thành Admin được
            string hash = BCrypt.Net.BCrypt.HashPassword(password);
            bool created = _userRepository.CreateUser(username, hash, "Player");

            // created = false nghĩa là có người khác vừa đăng ký trùng tên ngay giữa Bước 2 và Bước 3
            return created
                ? (true, "")
                : (false, "Tài khoản đã tồn tại, hãy chọn tên khác!");
        }
    }
} 